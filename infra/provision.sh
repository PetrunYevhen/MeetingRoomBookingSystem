#!/usr/bin/env bash
# Provisions every Azure resource this project runs on (see ADR 0001's deployment
# topology and README's "Deploy to Azure" section). Idempotent-ish: `az ... create`
# on most of these resource types is create-or-update, so re-running after a partial
# failure is safe; the exceptions (role assignment, federated credential) are already
# guarded or safe to re-run in practice.
#
# Requires: az CLI logged in to the target subscription, gh CLI logged in with
# repo+workflow scope on the GitHub repo. Run once, interactively, from repo root.
set -euo pipefail

RESOURCE_GROUP="rg-meeting-room-booking"
LOCATION="swedencentral"          # SQL Basic tier + App Service + SignalR all available here
SWA_LOCATION="westeurope"          # Static Web Apps aren't offered in Sweden Central
GITHUB_REPO="PetrunYevhen/MeetingRoomBookingSystem"

# Globally-unique DNS names (SQL server, Web App, SignalR, Static Web App) need a
# per-deployment suffix. Generate once and reuse for every resource below.
SUFFIX="${MRB_SUFFIX:-$(openssl rand -hex 3)}"
echo "Using suffix: $SUFFIX (export MRB_SUFFIX to pin it across re-runs)"

SQL_SERVER="sql-mrb-$SUFFIX"
SQL_DB="sqldb-meetingroombooking"
SQL_ADMIN_USER="mrbadmin"
SIGNALR_NAME="signalr-mrb-$SUFFIX"
PLAN_NAME="asp-mrb-$SUFFIX"
WEBAPP_NAME="api-mrb-$SUFFIX"
APPINSIGHTS_NAME="appi-mrb-$SUFFIX"
SWA_NAME="swa-mrb-$SUFFIX"
APP_REGISTRATION_NAME="gh-actions-mrb-deploy"

# --- Resource group -----------------------------------------------------------
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

# --- Azure SQL: logical server + Basic-tier database ---------------------------
SQL_ADMIN_PASSWORD="Mrb$(openssl rand -hex 12)!A9"
az sql server create \
  --name "$SQL_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --admin-user "$SQL_ADMIN_USER" \
  --admin-password "$SQL_ADMIN_PASSWORD"

az sql server firewall-rule create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER" \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

az sql db create \
  --resource-group "$RESOURCE_GROUP" \
  --server "$SQL_SERVER" \
  --name "$SQL_DB" \
  --edition Basic \
  --capacity 5

SQL_CONNECTION_STRING="Server=tcp:${SQL_SERVER}.database.windows.net,1433;Initial Catalog=${SQL_DB};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# --- Azure SignalR Service (Free tier, Default mode) ----------------------------
az signalr create \
  --name "$SIGNALR_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku Free_F1 \
  --service-mode Default

SIGNALR_CONNECTION_STRING=$(az signalr key list --name "$SIGNALR_NAME" --resource-group "$RESOURCE_GROUP" --query primaryConnectionString -o tsv)

# --- Backend: Linux App Service Plan (Free F1) + Web App ------------------------
az appservice plan create \
  --name "$PLAN_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku F1 \
  --is-linux

az webapp create \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --plan "$PLAN_NAME" \
  --runtime "DOTNETCORE:9.0"

# --- Application Insights (codeless auto-instrumentation) ----------------------
az provider register --namespace Microsoft.Insights
az provider register --namespace Microsoft.OperationalInsights

az monitor app-insights component create \
  --app "$APPINSIGHTS_NAME" \
  --location "$LOCATION" \
  --resource-group "$RESOURCE_GROUP" \
  --application-type web

APPINSIGHTS_CONNECTION_STRING=$(az monitor app-insights component show --app "$APPINSIGHTS_NAME" --resource-group "$RESOURCE_GROUP" --query connectionString -o tsv)

# --- Frontend: Static Web App (Free tier) ---------------------------------------
az staticwebapp create \
  --name "$SWA_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$SWA_LOCATION" \
  --sku Free

SWA_HOSTNAME=$(az staticwebapp show --name "$SWA_NAME" --resource-group "$RESOURCE_GROUP" --query defaultHostname -o tsv)
SWA_TOKEN=$(az staticwebapp secrets list --name "$SWA_NAME" --resource-group "$RESOURCE_GROUP" --query properties.apiKey -o tsv)

# --- Backend application settings ------------------------------------------------
JWT_SIGNING_KEY=$(openssl rand -base64 48 | tr -d '\n')
ADMIN_EMAIL="admin@meetingroombooking.local"
ADMIN_PASSWORD="Mrb$(openssl rand -hex 10)!Q7"

az webapp config appsettings set \
  --name "$WEBAPP_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    "ConnectionStrings__Default=$SQL_CONNECTION_STRING" \
    "Jwt__SigningKey=$JWT_SIGNING_KEY" \
    "Admin__Email=$ADMIN_EMAIL" \
    "Admin__Password=$ADMIN_PASSWORD" \
    "Cors__AllowedOrigins__0=https://$SWA_HOSTNAME" \
    "Azure__SignalR__ConnectionString=$SIGNALR_CONNECTION_STRING" \
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$APPINSIGHTS_CONNECTION_STRING" \
    "ApplicationInsightsAgent_EXTENSION_VERSION=~3" \
    "WEBSITE_RUN_FROM_PACKAGE=1"

# --- GitHub Actions OIDC identity, scoped to only this resource group ----------
APP_ID=$(az ad app create --display-name "$APP_REGISTRATION_NAME" --query appId -o tsv)
az ad sp create --id "$APP_ID"

# NOTE: this account's GitHub OIDC tokens include immutable owner/repo IDs in the
# subject claim (`repo:OWNER@OWNERID/REPO@REPOID:ref:...`), not the plain
# `repo:OWNER/REPO:ref:...` form GitHub's own docs lead with. If `azure/login`
# fails with AADSTS700213, its error message quotes the exact subject GitHub
# actually sent — use that verbatim rather than guessing the format.
GITHUB_OIDC_SUBJECT="repo:PetrunYevhen@181228492/MeetingRoomBookingSystem@1360308431:ref:refs/heads/main"

az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters "{
    \"name\": \"gh-main\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"${GITHUB_OIDC_SUBJECT}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)

az role assignment create \
  --assignee "$APP_ID" \
  --role Contributor \
  --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}"

# --- GitHub secrets and variables -----------------------------------------------
gh secret set AZURE_CLIENT_ID -b "$APP_ID" --repo "$GITHUB_REPO"
gh secret set AZURE_TENANT_ID -b "$TENANT_ID" --repo "$GITHUB_REPO"
gh secret set AZURE_SUBSCRIPTION_ID -b "$SUBSCRIPTION_ID" --repo "$GITHUB_REPO"
gh secret set AZURE_SQL_CONNECTION_STRING -b "$SQL_CONNECTION_STRING" --repo "$GITHUB_REPO"
gh secret set AZURE_STATIC_WEB_APPS_API_TOKEN -b "$SWA_TOKEN" --repo "$GITHUB_REPO"

gh variable set VITE_API_BASE_URL -b "https://${WEBAPP_NAME}.azurewebsites.net" --repo "$GITHUB_REPO"
gh variable set AZURE_SQL_SERVER_NAME -b "$SQL_SERVER" --repo "$GITHUB_REPO"
gh variable set AZURE_WEBAPP_NAME -b "$WEBAPP_NAME" --repo "$GITHUB_REPO"
gh variable set AZURE_RESOURCE_GROUP -b "$RESOURCE_GROUP" --repo "$GITHUB_REPO"

cat <<EOF

Provisioning complete.
  Backend:      https://${WEBAPP_NAME}.azurewebsites.net
  Frontend:     https://${SWA_HOSTNAME}
  Resource group: ${RESOURCE_GROUP} (${LOCATION})

Push to main to trigger .github/workflows/deploy.yml, or run it manually:
  gh workflow run deploy.yml --repo ${GITHUB_REPO}

To tear everything down once review is done:
  az group delete --name ${RESOURCE_GROUP} --yes
  az ad app delete --id ${APP_ID}
EOF
