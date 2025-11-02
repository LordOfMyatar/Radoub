# Test WAV file format details
param(
    [Parameter(Mandatory=$true)]
    [string]$WavFile
)

if (-not (Test-Path $WavFile)) {
    Write-Error "File not found: $WavFile"
    exit 1
}

$bytes = [System.IO.File]::ReadAllBytes($WavFile)

# Check RIFF header
$riff = [System.Text.Encoding]::ASCII.GetString($bytes[0..3])
if ($riff -ne "RIFF") {
    Write-Error "Not a valid WAV file (missing RIFF header)"
    exit 1
}

# Check WAVE format
$wave = [System.Text.Encoding]::ASCII.GetString($bytes[8..11])
if ($wave -ne "WAVE") {
    Write-Error "Not a valid WAV file (missing WAVE marker)"
    exit 1
}

# Find fmt chunk
for ($i = 12; $i -lt $bytes.Length - 4; $i++) {
    $chunk = [System.Text.Encoding]::ASCII.GetString($bytes[$i..($i+3)])
    if ($chunk -eq "fmt ") {
        $fmtStart = $i + 8

        # Audio format (2 bytes): 1 = PCM, 2 = ADPCM, etc.
        $audioFormat = [BitConverter]::ToUInt16($bytes, $fmtStart)

        # Number of channels (2 bytes)
        $channels = [BitConverter]::ToUInt16($bytes, $fmtStart + 2)

        # Sample rate (4 bytes)
        $sampleRate = [BitConverter]::ToUInt32($bytes, $fmtStart + 4)

        # Bits per sample (2 bytes) - offset 14 in fmt chunk
        $bitsPerSample = [BitConverter]::ToUInt16($bytes, $fmtStart + 14)

        Write-Host "WAV File Analysis: $WavFile"
        Write-Host "================================"
        Write-Host "Audio Format: $audioFormat $(if ($audioFormat -eq 1) { '(PCM - SUPPORTED)' } else { '(Non-PCM - NOT SUPPORTED by SoundPlayer)' })"
        Write-Host "Channels: $channels"
        Write-Host "Sample Rate: $sampleRate Hz"
        Write-Host "Bits per Sample: $bitsPerSample"
        Write-Host ""

        if ($audioFormat -eq 1) {
            Write-Host "✅ This file SHOULD work with System.Media.SoundPlayer"
        } else {
            Write-Host "❌ This file will NOT work with System.Media.SoundPlayer"
            Write-Host "   Format code $audioFormat indicates non-PCM encoding"
            Write-Host "   Common codes: 2=ADPCM, 3=IEEE Float, 6=A-law, 7=µ-law"
        }

        break
    }
}
