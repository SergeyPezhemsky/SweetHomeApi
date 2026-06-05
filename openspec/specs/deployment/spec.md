# Deployment Specification

## Requirements

### Requirement: Reverse Proxy Routing
The deployed SweetHome system SHALL route browser, API, Swagger, and account traffic through nginx.

#### Scenario: API route
- GIVEN nginx is configured from `_deploy/sweethome-api.nginx.conf`
- WHEN a request targets `/api/`
- THEN nginx proxies the request to the ASP.NET Core API on localhost port 5000.

#### Scenario: SPA fallback
- GIVEN nginx is configured from `_deploy/sweethome-api.nginx.conf`
- WHEN a request does not match an API, account, or swagger route
- THEN nginx serves the SPA fallback `index.html`.

### Requirement: HTTPS
The deployed system SHALL redirect HTTP traffic to HTTPS.

#### Scenario: HTTP request
- GIVEN a request arrives on port 80
- WHEN nginx receives the request
- THEN nginx redirects the request to the same host and URI over HTTPS.
