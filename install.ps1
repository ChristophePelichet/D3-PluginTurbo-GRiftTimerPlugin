#Requires -Version 5.0
<#
.SYNOPSIS
    Installe GRiftTimerPlugin dans TurboHUD.

.DESCRIPTION
    Copie GRiftTimerPlugin.cs dans le dossier plugins\GRiftTimer de TurboHUD.
    Détecte automatiquement le chemin TurboHUD ou accepte un chemin personnalisé
    via le paramètre -TurboHudPath.

.PARAMETER TurboHudPath
    Chemin racine de TurboHUD (dossier contenant TurboHUD.exe).
    Si omis, le script cherche dans les emplacements par défaut.

.EXAMPLE
    .\install.ps1
    .\install.ps1 -TurboHudPath "C:\TurboHUD"
#>

param(
    [string]$TurboHudPath = ""
)

$ErrorActionPreference = "Stop"

# ── Chemins par défaut à chercher ──────────────────────────────────────────
$defaultPaths = @(
    "$env:USERPROFILE\Games\TurboHub\installer\TurboHUD_LightningMOD_For_D3\TurboHUD",
    "D:\Games\TurboHub\installer\TurboHUD_LightningMOD_For_D3\TurboHUD",
    "C:\Games\TurboHub\installer\TurboHUD_LightningMOD_For_D3\TurboHUD",
    "C:\TurboHUD",
    "D:\TurboHUD"
)

# ── Résolution du chemin TurboHUD ──────────────────────────────────────────
if ($TurboHudPath -ne "") {
    if (-not (Test-Path $TurboHudPath)) {
        Write-Error "Chemin introuvable : $TurboHudPath"
        exit 1
    }
} else {
    foreach ($path in $defaultPaths) {
        # Accept folder if it contains TurboHUD_LightningMOD.exe or TurboHUD.exe
        if ((Test-Path (Join-Path $path "TurboHUD_LightningMOD.exe")) -or
            (Test-Path (Join-Path $path "TurboHUD.exe"))) {
            $TurboHudPath = $path
            break
        }
    }
    if ($TurboHudPath -eq "") {
        Write-Host ""
        Write-Host "TurboHUD introuvable dans les emplacements par defaut." -ForegroundColor Yellow
        Write-Host "Emplacements verifies :"
        $defaultPaths | ForEach-Object { Write-Host "  - $_" }
        Write-Host ""
        $TurboHudPath = Read-Host "Entrez le chemin complet vers le dossier TurboHUD"
        if (-not (Test-Path $TurboHudPath)) {
            Write-Error "Chemin introuvable : $TurboHudPath"
            exit 1
        }
    }
}

Write-Host ""
Write-Host "=== Installation de GRiftTimerPlugin ===" -ForegroundColor Cyan
Write-Host "Dossier TurboHUD : $TurboHudPath"
Write-Host ""

# ── Destination ────────────────────────────────────────────────────────────
$pluginDest = Join-Path $TurboHudPath "plugins\GRiftTimer"
$pluginSrc  = Join-Path $PSScriptRoot "plugins\GRiftTimer\GRiftTimerPlugin.cs"

# Vérification fichier source
if (-not (Test-Path $pluginSrc)) {
    Write-Error "Fichier source introuvable : $pluginSrc"
    exit 1
}

# Création du dossier si besoin
if (-not (Test-Path $pluginDest)) {
    New-Item -ItemType Directory -Path $pluginDest -Force | Out-Null
    Write-Host "Dossier cree : $pluginDest" -ForegroundColor Green
}

# Sauvegarde de l'ancienne version si elle existe
$destFile = Join-Path $pluginDest "GRiftTimerPlugin.cs"
if (Test-Path $destFile) {
    $backup = "$destFile.bak"
    Copy-Item $destFile $backup -Force
    Write-Host "Ancienne version sauvegardee : $backup" -ForegroundColor DarkYellow
}

# Copie
Copy-Item $pluginSrc $destFile -Force
Write-Host "Plugin installe : $destFile" -ForegroundColor Green

Write-Host ""
Write-Host "=== Installation terminee ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Prochaines etapes :" -ForegroundColor White
Write-Host "  1. Lancez (ou relancez) TurboHUD"
Write-Host "  2. Entrez dans un Greater Rift"
Write-Host "  3. La fenetre GR Timer apparait en haut a droite"
Write-Host ""
Write-Host "Raccourcis clavier :" -ForegroundColor White
Write-Host "  [T]  Masquer / afficher la fenetre"
Write-Host "  [R]  Reinitialiser l'historique"
Write-Host "  [S]  Ouvrir la configuration dans Notepad"
Write-Host ""
