#!/bin/bash
set -e

/opt/mssql/bin/sqlservr &
SQL_PID=$!

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
if [ ! -x "$SQLCMD" ]; then
    SQLCMD=/opt/mssql-tools/bin/sqlcmd
fi

echo "Waiting for SQL Server to accept connections..."
for i in $(seq 1 60); do
    if "$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" &> /dev/null; then
        echo "SQL Server is up."
        break
    fi
    sleep 2
done

echo "Ensuring database/login exist for DB_USER=$DB_USER, DB_NAME=$DB_NAME..."
"$SQLCMD" -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
    -v DB_NAME="$DB_NAME" DB_USER="$DB_USER" DB_PASSWORD="$DB_PASSWORD" \
    -i /usr/src/app/db-init/init.sql

wait "$SQL_PID"
