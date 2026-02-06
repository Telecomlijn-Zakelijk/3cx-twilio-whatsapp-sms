# Audiobestanden

Plaats hier je `.wav`-audiobestanden voor het call flow script.

## Vereist formaat

- **Type**: PCM WAV
- **Sample rate**: 8000 Hz (8 kHz) of 16000 Hz (16 kHz)
- **Bitdiepte**: 16-bit
- **Kanalen**: Mono (1 kanaal)

## Vereiste bestanden

| Bestand | Beschrijving |
|---|---|
| `sms_whatsapp_prompt.wav` | Welkomstbericht met uitleg over SMS |
| `landline_detected.wav` | Melding dat vast nummer is gedetecteerd |
| `enter_mobile.wav` | Vraag om mobiel nummer in te voeren |
| `you_entered.wav` | "U heeft ingevoerd:" |
| `is_this_correct.wav` | Bevestigingsvraag (druk 1 of 2) |
| `invalid_number.wav` | Melding ongeldig nummer |
| `sms_sent.wav` | Bevestiging SMS verzonden |
| `no_input.wav` | Melding geen invoer ontvangen |
| `goodbye.wav` | Afsluitbericht |
| `digit_0.wav` t/m `digit_9.wav` | Individuele cijfers (nul t/m negen) |

## Aanmaken met TTS

Je kunt audiobestanden genereren met:

- [Google Cloud Text-to-Speech](https://cloud.google.com/text-to-speech)
- [Amazon Polly](https://aws.amazon.com/polly/)
- [Azure TTS](https://azure.microsoft.com/nl-nl/products/ai-services/text-to-speech)

Converteren naar het juiste formaat met ffmpeg:

```bash
ffmpeg -i input.mp3 -ar 8000 -ac 1 -acodec pcm_s16le output.wav
```

## Plaatsing op 3CX server

Kopieer alle bestanden naar de map op je 3CX server:

```
Callflows/twiliowhatsappsms/
```

De exacte locatie is afhankelijk van je 3CX installatie (Linux of Windows).
