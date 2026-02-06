# 3CX Twilio WhatsApp SMS Script

> **3CX Call Flow Script** voor automatische SMS/WhatsApp-link verzending via Twilio met intelligente vaste lijn detectie.

Ontwikkeld door [Telecomlijn Zakelijk B.V.](https://www.telecomlijn.nl)

---

## Inhoudsopgave

- [Wat doet dit script?](#wat-doet-dit-script)
- [Hoe werkt het?](#hoe-werkt-het)
- [Vereisten](#vereisten)
- [Installatie](#installatie)
  - [Stap 1: Twilio instellen](#stap-1-twilio-instellen)
  - [Stap 2: Script configureren](#stap-2-script-configureren)
  - [Stap 3: Audiobestanden aanmaken](#stap-3-audiobestanden-aanmaken)
  - [Stap 4: Script uploaden naar 3CX](#stap-4-script-uploaden-naar-3cx)
  - [Stap 5: Call Flow koppelen](#stap-5-call-flow-koppelen)
- [Configuratie](#configuratie)
  - [Twilio instellingen](#twilio-instellingen)
  - [Timing instellingen](#timing-instellingen)
  - [SMS bericht aanpassen](#sms-bericht-aanpassen)
- [Gespreksverloop](#gespreksverloop)
  - [Mobiel nummer belt](#mobiel-nummer-belt)
  - [Vast nummer belt](#vast-nummer-belt)
- [Audiobestanden](#audiobestanden)
- [Nummerherkenning](#nummerherkenning)
- [DTMF-invoer](#dtmf-invoer)
- [Foutoplossing](#foutoplossing)
- [Veelgestelde vragen](#veelgestelde-vragen)
- [Licentie](#licentie)

---

## Wat doet dit script?

Dit 3CX Call Processing Script stuurt automatisch een SMS met een WhatsApp-link naar bellers. Het script herkent of de beller een **mobiel** of **vast** nummer heeft:

- **Mobiel nummer**: SMS wordt direct naar het bellende nummer gestuurd.
- **Vast nummer**: De beller wordt gevraagd om een mobiel nummer in te voeren via het toetsenbord (DTMF), waarna de SMS naar dat nummer wordt verzonden.

De SMS wordt verstuurd via de **Twilio API**.

---

## Hoe werkt het?

```
Inkomend gesprek
       |
       v
+--------------+
| Nummer ophalen|
| (Caller ID)  |
+------+-------+
       |
       v
+--------------+     Mobiel (+316...)       +-------------+
|  Classificeer +-------------------------->| Speel prompt|
|  nummertype   |                           | + Stuur SMS |
+------+-------+                           +-------------+
       |
       | Vast nummer
       v
+--------------+
| "Vast nummer  |
|  gedetecteerd"|
+------+-------+
       |
       v
+--------------+     Geldig 06-nummer      +-------------+
| Vraag mobiel  +------------------------->| Bevestig +   |
| nummer (DTMF) |                          | Stuur SMS   |
+------+-------+                           +-------------+
       |
       | Ongeldig / geen invoer
       v
+--------------+
| Max 3 pogingen|
| daarna afsluit|
+--------------+
```

---

## Vereisten

| Vereiste | Details |
|---|---|
| **3CX** | Versie 18 of hoger met Call Flow Designer |
| **Twilio account** | [Aanmaken op twilio.com](https://www.twilio.com/) |
| **Twilio SMS** | Een Twilio telefoonnummer of Alphanumeric Sender ID |
| **.wav audiobestanden** | Zie sectie [Audiobestanden](#audiobestanden) |

---

## Installatie

### Stap 1: Twilio instellen

1. Maak een account aan op [twilio.com](https://www.twilio.com/).
2. Ga naar **Console Dashboard** en noteer:
   - **Account SID** (begint met `AC...`)
   - **Auth Token**
3. Koop een telefoonnummer of stel een **Alphanumeric Sender ID** in (bijv. `Telecomlijn`).

### Stap 2: Script configureren

Open `twiliowhatsappsms.cs` en pas de configuratie bovenaan het bestand aan:

```csharp
private const string TwilioAccountSid = "JOUW_TWILIO_ACCOUNT_SID";
private const string TwilioAuthToken = "JOUW_TWILIO_AUTH_TOKEN";
private const string TwilioFromNumber = "JOUW_TWILIO_AFZENDERNAAM_OF_NUMMER";
private const string SmsMessage = "Stel direct uw vraag via WhatsApp: https://jouw-domein.nl/link";
```

| Veld | Voorbeeld | Toelichting |
|---|---|---|
| `TwilioAccountSid` | `AC1234567890abcdef...` | Je Twilio Account SID |
| `TwilioAuthToken` | `abcdef1234567890...` | Je Twilio Auth Token |
| `TwilioFromNumber` | `Telecomlijn` of `+31201234567` | Afzendernaam of Twilio nummer |
| `SmsMessage` | `Stel direct uw vraag...` | De inhoud van de SMS |

> **Belangrijk**: Zet **nooit** je echte Twilio credentials in een publiek Git-repository!

### Stap 3: Audiobestanden aanmaken

Maak de volgende `.wav`-bestanden aan (PCM, 8kHz of 16kHz, mono). Je kunt hiervoor een TTS-dienst gebruiken (bijv. Google TTS, Amazon Polly) of zelf insprekken.

Plaats alle bestanden in de map `Callflows/twiliowhatsappsms/` op je 3CX server.

Zie de [volledige lijst](#audiobestanden) hieronder.

### Stap 4: Script uploaden naar 3CX

1. Open de **3CX Management Console**.
2. Ga naar **Advanced** > **Call Flow Apps** (of **Call Flow Designer**).
3. Klik op **Add/Upload** en upload `twiliowhatsappsms.cs`.
4. Het script wordt automatisch gecompileerd door 3CX.

Meer info: [3CX Call Processing Script documentatie](https://www.3cx.com/docs/manual/call-processing-script/)

### Stap 5: Call Flow koppelen

1. Ga naar **Inbound Rules** of een **Digital Receptionist / IVR**.
2. Koppel het script als **Call Flow Destination** aan de gewenste route.
3. Bellers die op deze route binnenkomen worden nu verwerkt door het script.

---

## Configuratie

### Twilio instellingen

De Twilio-configuratie staat bovenaan het bestand:

```csharp
private const string TwilioAccountSid = "JOUW_TWILIO_ACCOUNT_SID";
private const string TwilioAuthToken = "JOUW_TWILIO_AUTH_TOKEN";
private const string TwilioFromNumber = "JOUW_TWILIO_AFZENDERNAAM_OF_NUMMER";
private const string SmsMessage = "Jouw SMS tekst hier";
```

### Timing instellingen

| Instelling | Standaard | Beschrijving |
|---|---|---|
| `MaxRetries` | `3` | Maximaal aantal pogingen voor nummervraag |
| `InputTimeoutMs` | `30000` (30s) | Totale timeout voor nummerinvoer |
| `InterDigitTimeoutMs` | `5000` (5s) | Timeout tussen individuele cijfers |
| `ConfirmationTimeoutMs` | `10000` (10s) | Timeout voor bevestiging (toets 1 of 2) |

### SMS bericht aanpassen

Wijzig de `SmsMessage` constante:

```csharp
private const string SmsMessage = "Jouw aangepaste bericht hier";
```

Houd rekening met de SMS-limiet van **160 tekens** per segment (of 70 bij Unicode).

---

## Gespreksverloop

### Mobiel nummer belt

1. Beller belt in met een mobiel nummer (bijv. `+316 12345678`)
2. Script herkent het als Nederlands mobiel nummer
3. Audio: *"Wij sturen u een SMS met een WhatsApp-link..."*
4. SMS wordt verstuurd naar het bellende nummer
5. Audio: *"De SMS is verzonden. Tot ziens."*

### Vast nummer belt

1. Beller belt in met een vast nummer (bijv. `+31 20 1234567`)
2. Script detecteert dat het een vast nummer is
3. Audio: *"U belt vanaf een vast nummer..."*
4. Audio: *"Voer uw mobiele nummer in, gevolgd door hekje"*
5. Beller voert 10-cijferig nummer in via toetsenbord (bijv. `0612345678`)
6. Audio: *"U heeft ingevoerd: 0-6-1-2-3-4-5-6-7-8"* (elk cijfer afzonderlijk)
7. Audio: *"Klopt dit? Druk 1 voor ja, 2 voor opnieuw"*
   - **Toets 1** (of timeout): Nummer wordt bevestigd
   - **Toets 2**: Terug naar stap 4
8. SMS wordt verstuurd naar het ingevoerde nummer
9. Audio: *"De SMS is verzonden. Tot ziens."*

Bij ongeldig nummer of geen invoer: maximaal **3 pogingen**, daarna wordt het gesprek beeindigd.

---

## Audiobestanden

Alle bestanden moeten in `.wav`-formaat zijn (PCM, 8kHz of 16kHz, mono) en geplaatst worden in:

```
Callflows/twiliowhatsappsms/
```

| Bestandsnaam | Inhoud (Nederlands) |
|---|---|
| `sms_whatsapp_prompt.wav` | "Wij sturen u een SMS met een link naar ons WhatsApp-nummer." |
| `landline_detected.wav` | "U belt vanaf een vast telefoonnummer. Om een SMS te ontvangen hebben wij uw mobiele nummer nodig." |
| `enter_mobile.wav` | "Voer uw 10-cijferige mobiele nummer in, gevolgd door het hekje." |
| `you_entered.wav` | "U heeft ingevoerd:" |
| `is_this_correct.wav` | "Klopt dit nummer? Druk 1 voor ja, druk 2 om opnieuw in te voeren." |
| `invalid_number.wav` | "Het ingevoerde nummer is ongeldig. Probeer het opnieuw." |
| `sms_sent.wav` | "De SMS is succesvol verzonden." |
| `no_input.wav` | "Er is geen invoer ontvangen." |
| `goodbye.wav` | "Bedankt voor het bellen. Tot ziens." |
| `digit_0.wav` t/m `digit_9.wav` | "nul", "een", "twee", "drie", "vier", "vijf", "zes", "zeven", "acht", "negen" |

### Tips voor audiobestanden

- Gebruik een professionele stem of TTS-dienst (Google Cloud TTS, Amazon Polly, Azure TTS)
- Formaat: **PCM WAV, 8000 Hz, 16-bit, mono** (standaard telefonie-kwaliteit)
- Houd berichten kort en duidelijk
- Test de audio op een echt telefoongesprek voordat je live gaat

---

## Nummerherkenning

Het script classificeert inkomende nummers als volgt:

| Type | Patroon | Voorbeeld | Actie |
|---|---|---|---|
| **Nederlands mobiel** | `+316XXXXXXXX` | `+31612345678` | Direct SMS sturen |
| **Nederlandse vaste lijn** | `+31` (niet `+316`) | `+31201234567` | Mobiel nummer vragen |
| **Internationaal** | `+XX...` (niet `+31`) | `+491701234567` | Direct SMS sturen |
| **Onbekend** | Overig | - | Behandelen als mobiel |

### Caller ID detectie

Het script probeert het nummer van de beller op te halen via meerdere bronnen (in volgorde):

1. `MyCall.ExternalParty` - het directe externe nummer
2. `AttachedData["extnumber"]` - doorgeschakeld nummer
3. `AttachedData["public_push_callerid"]` - push caller ID
4. `MyCall.Caller.CallerID` - standaard Caller ID

Interne extensies (4 cijfers of minder, zonder `+`) worden automatisch overgeslagen.

---

## DTMF-invoer

Het script ondersteunt twee soorten DTMF-invoer:

### Nummerinvoer (multi-digit)
- Maximaal **10 cijfers** (voor een volledig 06-nummer)
- Druk op **#** om eerder te bevestigen
- Druk op __*__ om opnieuw te beginnen
- Automatische bevestiging bij 10 cijfers

### Bevestiging (single-digit)
- **Toets 1**: Bevestig het nummer
- **Toets 2**: Voer opnieuw in
- **Timeout** (10 sec): Wordt behandeld als bevestiging

---

## Foutoplossing

### SMS wordt niet verzonden

| Probleem | Oplossing |
|---|---|
| Twilio credentials onjuist | Controleer `TwilioAccountSid` en `TwilioAuthToken` in de Twilio Console |
| Twilio account geen saldo | Controleer je Twilio saldo op [twilio.com/console](https://www.twilio.com/console) |
| Afzendernummer niet geconfigureerd | Controleer of `TwilioFromNumber` overeenkomt met een Twilio nummer of Sender ID |
| Nummer geblokkeerd | Controleer Twilio logs voor foutmeldingen |

### Audiobestanden werken niet

| Probleem | Oplossing |
|---|---|
| Geen geluid | Controleer of bestanden in `Callflows/twiliowhatsappsms/` staan |
| Vervormd geluid | Controleer formaat: PCM WAV, 8kHz, 16-bit, mono |
| Bestand niet gevonden | Controleer bestandsnamen (hoofdlettergevoelig op Linux) |

### Nummer wordt niet herkend

| Probleem | Oplossing |
|---|---|
| Altijd "vast nummer" | Controleer of het nummer correct in E.164-formaat binnenkomt |
| Geen Caller ID | SIP trunk moet Caller ID doorsturen; controleer trunk-instellingen |
| Intern nummer gedetecteerd | Extensies van 4 cijfers of minder worden overgeslagen |

### 3CX compilatiefout

| Probleem | Oplossing |
|---|---|
| Script compileert niet | Controleer of je 3CX v18+ gebruikt |
| Namespace-fout | De namespace moet `dummy` zijn (3CX vereiste) |
| Ontbrekende referenties | `CallFlow`, `TCX.Configuration` en `TCX.PBXAPI` zijn standaard beschikbaar |

### Logging bekijken

Het script logt uitgebreid via `MyCall.Info()` en `MyCall.Error()`. Bekijk de logs in:

- **3CX Management Console** > **Logs**
- Of via het logbestand op de 3CX server

Zoek op `TwilioWhatsAppSMS` om relevante logregels te vinden.

---

## Veelgestelde vragen

**V: Kan ik WhatsApp-berichten sturen in plaats van SMS?**
A: Dit script verstuurt een SMS met daarin een *link* naar WhatsApp. Voor directe WhatsApp-berichten heb je de Twilio WhatsApp Business API nodig met een goedgekeurd template.

**V: Werkt dit met internationale nummers?**
A: Ja, internationale nummers worden herkend en ontvangen direct een SMS. Let op dat Twilio kosten per land verschillen.

**V: Kan ik de SMS-tekst dynamisch maken?**
A: Ja, je kunt de `SmsMessage` constante vervangen door logica die het bericht dynamisch opbouwt, bijv. met de naam van het bedrijf of een referentienummer.

**V: Hoeveel kost een SMS via Twilio?**
A: Twilio rekent per SMS-segment. Prijzen varieren per land. Zie [twilio.com/sms/pricing](https://www.twilio.com/sms/pricing) voor actuele tarieven.

**V: Kan ik het aantal pogingen aanpassen?**
A: Ja, wijzig `MaxRetries` in het configuratieblok. Standaard is 3.

---

## Technische details

- **Taal**: C# (.cs)
- **Framework**: 3CX Call Flow SDK (`CallFlow`, `TCX.Configuration`, `TCX.PBXAPI`)
- **Base class**: `ScriptBase<TwilioWhatsAppSMSSender>`
- **Namespace**: `dummy` (vereist door 3CX)
- **Async**: Volledig asynchroon met `async/await` en `CancellationToken`
- **HTTP**: Directe Twilio REST API calls via `HttpClient` (geen SDK dependency)

---

## Licentie

Copyright (c) Telecomlijn Zakelijk B.V. Alle rechten voorbehouden.
