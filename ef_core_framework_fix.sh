# Entity Framework Core - Target Framework Problem lösen

echo "=== EF Core Target Framework Fix ==="

# 1. Aktuelles Target Framework prüfen
echo "Prüfe aktuelles Target Framework..."
dotnet --version
echo ""

# 2. Projekt-Datei (.csproj) anzeigen
echo "Zeige aktuelle .csproj Konfiguration:"
echo "Suche nach <TargetFramework> in der .csproj Datei..."
find . -name "*.csproj" -exec grep -H "TargetFramework" {} \;
echo ""

# 3. Empfohlene Target Frameworks für EF Core
echo "=== Kompatible Target Frameworks für EF Core ==="
echo "Für EF Core 6.x:  net6.0"
echo "Für EF Core 7.x:  net7.0" 
echo "Für EF Core 8.x:  net8.0"
echo "Für EF Core 9.x:  net9.0"
echo ""

# 4. Automatischer Fix für häufige Probleme
echo "=== Automatische Reparatur ==="

# Alte EF Pakete entfernen
echo "Entferne alte/inkompatible Entity Framework Pakete..."
dotnet remove package EntityFramework 2>/dev/null || true
dotnet remove package System.Data.Entity 2>/dev/null || true

# Korrekte EF Core Pakete installieren
echo "Installiere kompatible EF Core Pakete..."
dotnet add package Microsoft.EntityFrameworkCore --version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.0.8
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.8

# Projekt neu erstellen
echo "Führe Clean & Rebuild durch..."
dotnet clean
dotnet restore
dotnet build

echo ""
echo "=== Manuelle Schritte (falls automatischer Fix nicht hilft) ==="
echo ""
echo "1. Öffne deine .csproj Datei"
echo "2. Ändere <TargetFramework> von:"
echo "   - net48, net472, netstandard2.0 (veraltet)"
echo "   zu:"
echo "   - net8.0 (empfohlen)"
echo ""
echo "3. Beispiel .csproj Konfiguration:"
echo "<Project Sdk=\"Microsoft.NET.Sdk\">"
echo "  <PropertyGroup>"
echo "    <TargetFramework>net8.0</TargetFramework>"
echo "  </PropertyGroup>"
echo "</Project>"
echo ""
echo "4. Nach Änderung der .csproj:"
echo "   dotnet restore"
echo "   dotnet build"
echo ""

# EF Tools neu installieren
echo "Installiere EF Tools neu..."
dotnet tool uninstall --global dotnet-ef 2>/dev/null || true
dotnet tool install --global dotnet-ef

echo "=== Überprüfung der Installation ==="
dotnet ef --version

echo ""
echo "Falls der Fehler weiterhin auftritt:"
echo "- Prüfe, ob du .NET Framework statt .NET Core/5+/6+/7+/8+ verwendest"
echo "- EF Core funktioniert NICHT mit .NET Framework 4.x"
echo "- Verwende .NET 6.0 oder höher für moderne EF Core Versionen"