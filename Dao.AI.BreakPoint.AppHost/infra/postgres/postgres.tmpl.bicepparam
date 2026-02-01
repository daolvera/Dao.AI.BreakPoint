using './postgres-containerapp.module.bicep'

param breakpointenv_outputs_azure_container_apps_environment_default_domain = '{{ .Env.BREAKPOINTENV_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN }}'
param breakpointenv_outputs_azure_container_apps_environment_id = '{{ .Env.BREAKPOINTENV_AZURE_CONTAINER_APPS_ENVIRONMENT_ID }}'
param postgres_password_value = '{{ securedParameter "postgres_password" }}'
