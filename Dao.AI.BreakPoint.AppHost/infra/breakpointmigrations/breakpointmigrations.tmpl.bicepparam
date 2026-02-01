using './breakpointmigrations-containerapp.module.bicep'

param breakpointenv_outputs_azure_container_apps_environment_default_domain = '{{ .Env.BREAKPOINTENV_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN }}'
param breakpointenv_outputs_azure_container_apps_environment_id = '{{ .Env.BREAKPOINTENV_AZURE_CONTAINER_APPS_ENVIRONMENT_ID }}'
param breakpointenv_outputs_azure_container_registry_endpoint = '{{ .Env.BREAKPOINTENV_AZURE_CONTAINER_REGISTRY_ENDPOINT }}'
param breakpointenv_outputs_azure_container_registry_managed_identity_id = '{{ .Env.BREAKPOINTENV_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID }}'
param breakpointmigrations_containerimage = '{{ .Image }}'
param postgres_password_value = '{{ securedParameter "postgres_password" }}'
