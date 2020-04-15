# BerBo-T

## Dependencies
1. PostgreSQL - I'm running v12.2.
2. .NET Core - I'm running 3.1.201.

## Setup
1. `psql -h host -U username -d postgres -a -f provision.sql` - This nukes everything, so don't run it on accident. Haven't added a migration system yet.

## Run it
1. Copy `src/BerBo-T/BerbotConfiguration.Template.cs_` to `src/BerBo-T/BerbotConfiguration.cs`. 
2. Fill it out.
3. `dotnet run`
