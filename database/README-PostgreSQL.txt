VITEC Scoreboard v0.4 - PostgreSQL bootstrap

Recommended database name:
    vitec_scoreboard

Recommended application login:
    vsapp

Example commands to run as a PostgreSQL administrator:

    CREATE ROLE vsapp WITH LOGIN PASSWORD 'CHANGE_THIS_PASSWORD';
    CREATE DATABASE vitec_scoreboard OWNER vsapp;

VS creates its application tables and indexes when you POST to:
    http://localhost:5000/api/db/initialize

Do not commit a real database password to GitHub or appsettings.json.

Recommended application connection string format:
    Host=localhost;Port=5432;Database=vitec_scoreboard;Username=vsapp;Password=YOUR_PASSWORD;Pooling=true;Maximum Pool Size=50

For Windows development, use:
    .\scripts\Start-VS-With-Postgres.ps1 -Password "YOUR_PASSWORD"

If PostgreSQL is remote, also supply -HostName and ensure PostgreSQL/firewall access is configured.
