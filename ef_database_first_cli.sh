# ===================================================
# Database First Entity Framework Core - CLI Script
# ===================================================

echo "=== Database First EF Core Setup ==="

# 1. Neue Klassenbibliothek erstellen
echo "Erstelle neue Klassenbibliothek..."
dotnet new classlib -n "TradingBot.Data" -f net9.0
cd TradingBot.Data

# 2. Erforderliche NuGet-Pakete installieren
echo "Installiere EF Core Pakete..."
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 9.0.8
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.8
dotnet add package Microsoft.EntityFrameworkCore.Tools --version 9.0.8

# 3. EF Tools global installieren (falls noch nicht vorhanden)
echo "Installiere EF Tools..."
dotnet tool install --global dotnet-ef --version 9.0.8

# 4. Scaffold Database zu Code (Database First)
echo "Generiere Modelle aus der Datenbank..."

# WICHTIG: Passe diese Connection String an deine Datenbank an!
$CONNECTION_STRING = "Server=localhost;Database=TradingBotDB;Trusted_Connection=true;TrustServerCertificate=true;"

# Basic Scaffold Command
dotnet ef dbcontext scaffold "$CONNECTION_STRING" Microsoft.EntityFrameworkCore.SqlServer

# === ERWEITERTE SCAFFOLD OPTIONEN ===

# Mit spezifischen Tabellen (nur bestimmte Tabellen)
# dotnet ef dbcontext scaffold "$CONNECTION_STRING" Microsoft.EntityFrameworkCore.SqlServer --table Users --table Orders --table Trades

# Mit benutzerdefinierten Namen und Ordnern
dotnet ef dbcontext scaffold "$CONNECTION_STRING" Microsoft.EntityFrameworkCore.SqlServer \
    --context "TradingBotContext" \
    --context-dir "Context" \
    --output-dir "Models" \
    --namespace "TradingBot.Data.Models" \
    --context-namespace "TradingBot.Data.Context" \
    --force

# Mit zusätzlichen Optionen
# dotnet ef dbcontext scaffold "$CONNECTION_STRING" Microsoft.EntityFrameworkCore.SqlServer \
#     --context "TradingBotContext" \
#     --context-dir "Context" \
#     --output-dir "Models" \
#     --namespace "TradingBot.Data.Models" \
#     --context-namespace "TradingBot.Data.Context" \
#     --data-annotations \
#     --use-database-names \
#     --force \
#     --no-pluralize

echo ""
echo "=== Scaffold Optionen Erklärung ==="
echo "--context: Name des DbContext"
echo "--context-dir: Ordner für DbContext"
echo "--output-dir: Ordner für Model-Klassen"
echo "--namespace: Namespace für Models"
echo "--context-namespace: Namespace für Context"
echo "--data-annotations: Verwendet Data Annotations statt Fluent API"
echo "--use-database-names: Behält Datenbank-Tabellennamen bei"
echo "--force: Überschreibt existierende Dateien"
echo "--no-pluralize: Deaktiviert Pluralisierung von Tabellennamen"
echo "--table: Nur spezifische Tabellen scaffolden"

# 5. Projekt bauen
echo "Baue Projekt..."
dotnet build

echo ""
echo "=== Alternative Connection Strings ==="
echo "SQL Server (Windows Auth):"
echo "Server=localhost;Database=TradingBotDB;Trusted_Connection=true;TrustServerCertificate=true;"
echo ""
echo "SQL Server (SQL Auth):"
echo "Server=localhost;Database=TradingBotDB;User Id=sa;Password=YourPassword;TrustServerCertificate=true;"
echo ""
echo "LocalDB:"
echo "Server=(localdb)\\mssqllocaldb;Database=TradingBotDB;Trusted_Connection=true;"
echo ""
echo "Azure SQL:"
echo "Server=tcp:yourserver.database.windows.net,1433;Database=TradingBotDB;User ID=username;Password=password;Encrypt=True;"

echo ""
echo "=== Nächste Schritte ==="
echo "1. Connection String in appsettings.json konfigurieren"
echo "2. DbContext in Hauptprojekt registrieren:"
echo "   services.AddDbContext<TradingBotContext>(options =>"
echo "       options.UseSqlServer(connectionString));"
echo ""
echo "3. Bei Datenbankänderungen erneut scaffolden:"
echo "   dotnet ef dbcontext scaffold \"ConnectionString\" Microsoft.EntityFrameworkCore.SqlServer --force"
echo ""
echo "4. Projekt als Referenz hinzufügen:"
echo "   dotnet add reference ../TradingBot.Data/TradingBot.Data.csproj"

# Beispiel für ein komplettes Setup-Script
echo ""
echo "=== Komplettes Beispiel-Setup ==="
echo "# 1. Lösung erstellen"
echo "dotnet new sln -n TradingBotSolution"
echo ""
echo "# 2. Projekte erstellen"
echo "dotnet new wpf -n TradingBot.WPF -f net9.0-windows"
echo "dotnet new classlib -n TradingBot.Data -f net9.0"
echo ""
echo "# 3. Projekte zur Lösung hinzufügen"
echo "dotnet sln add TradingBot.WPF/TradingBot.WPF.csproj"
echo "dotnet sln add TradingBot.Data/TradingBot.Data.csproj"
echo ""
echo "# 4. Referenz hinzufügen"
echo "dotnet add TradingBot.WPF/TradingBot.WPF.csproj reference TradingBot.Data/TradingBot.Data.csproj"