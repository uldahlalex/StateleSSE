export PATH="$PATH:$HOME/.dotnet/tools"

# 1. Delete migration files                                                   
  rm -rf ./Migrations/
                                                                                
  # 2. Drop the DB (wipes __EFMigrationsHistory too)
  dotnet ef database drop --force || true

  # 3. Create single migration from current model
  dotnet ef migrations add Init 

  # 4. Apply it
  dotnet ef database update 
