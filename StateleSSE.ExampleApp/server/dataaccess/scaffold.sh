#!/bin/bash
set -a
source .env
set +a


# Run EF Core scaffolding
dotnet tool install -g dotnet-ef
dotnet ef dbcontext scaffold "$CONN_STR"  Npgsql.EntityFrameworkCore.PostgreSQL   --context MyDbContext     --no-onconfiguring        --schema kahoot   --force
