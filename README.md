# TFM Accounting

.NET 10 / WPF számla- és kötelezettségnyilvántartó. Az első verzió HUF-tételeket kezel, alkalmazásbeli felhasználókezelés nélkül.

## Indítás helyi adatbázissal

A fejlesztéshez megmaradó **TFM_ACCOUNTING_Development** adatbázist használjuk a **(localdb)\MSSQLLocalDB** példányon. A WPF alkalmazás ezt választja alapértelmezésként; külön kapcsolatbeállítás nem szükséges. A mostani gépen az adatbázis már létrejött, és tartalmazza a kezdő törzsadatokat.

Visual Studióban nyisd meg a `TFM-ACCOUNTING.slnx` fájlt, válaszd indítási projektnek a **TFM-ACCOUNTING.Desktop** projektet, majd indítsd el. Az alkalmazás a **Tételek** képernyővel indul. A **Saját cégek** és **Partnerek** képernyőn cégek és partnerek rögzíthetők; a **Beállítások** alatt a hat törzsadatcsoport kezelhető. A felvett adatok az alkalmazás bezárása után is megmaradnak.

Másik fejlesztői gépen, SQL Server Express LocalDB és .NET 10 SDK telepítése után:

```powershell
./scripts/Initialize-LocalDatabase.ps1
dotnet run --project TFM-ACCOUNTING.Desktop
```

A script kizárólag a név szerint rögzített helyi fejlesztési adatbázisra alkalmaz EF migrációkat. Meglévő adatot nem töröl, ismételten is futtatható. Az alkalmazás induláskor nem hoz létre vagy frissít adatbázissémát.

## Elkészült funkciók

- **Saját cégek:** keresés névre/rövid névre/adószámra, aktív állapot szerinti szűrés, rendezés és 25-ös lapozás; felvétel, részletek, módosítás, inaktiválás és újraaktiválás. Adószám-egyediség, opcionális rövid név, 2000 karakteres megjegyzés, automatikus belső azonosító, konkurenciavédelem.
- **Partnerek:** keresés, állapot- és típusszűrés, rendezés, lapozás, felvétel, módosítás, aktiválás/inaktiválás. Egyedi adószám vagy belső azonosító, adatbázisból választott típus, azonos névnél meglévő partner megnyitása vagy megerősített külön mentés.
- **Tételek:** számlák, bérek, járulékok és egyéb költségek rögzítése és módosítása. Számlahivatkozások, archiválás, szűrés, lapozás, fizetendő és jóváírás összesítése, CSV export.
- **Beállítások:** költségkategóriák, partnertípusok, kötelezettségtípusok, számlatípusok, fizetési módok és rendezési típusok kezelése.
- **Adatréteg:** 12 entitás, idegen és egyedi kulcsok, verziójelzők, EF Core konfigurációk és első migráció.

A következő fejlesztési lépések a fizetések, jóváírások beszámítása és a NAS-os csatolmánykezelés.

## Projektek

- **Data:** entitások, EF Core 10.0.11, Azure SQL/SQL Server, DbContext, konfigurációk és migrációk.
- **Business:** `Response<T>`, DTO-k, validáció, üzleti szolgáltatások és hibakezelés.
- **Desktop:** WPF/MVVM felületek és alkalmazás-összeállítás.

A hivatkozási irány Desktop → Business → Data. A felület a Business szolgáltatásain keresztül kezeli az adatokat.

## Ellenőrzés

```powershell
dotnet build TFM-ACCOUNTING.slnx -c Release -m:1
./scripts/Verify-Foundation.ps1
./scripts/Verify-Foundation.ps1 -Integration
```

Az ellenőrző script az `obj` mappában készít segédprojektet; a solution három projektből áll. Vizsgálja az adatmodellt és a validációt, rendereli a Beállítások, Saját cégek és Partnerek nézetét. Az integrációs teszt külön, véletlen nevű LocalDB-adatbázisban próbálja ki a migrációkat, törzsadatokat, a cégek és partnerek kezelését, valamint a névegyezés megerősítését, majd eltávolítja kizárólag ezt az eldobható adatbázist. A megmaradó `TFM_ACCOUNTING_Development` adatbázist és a felvitt cégeket nem érinti.

## Későbbi Azure-átállás

A `TFM_ACCOUNTING_CONNECTION_STRING` környezeti változó felülírja a helyi alapértelmezést. Később ezen keresztül állítjuk be az Azure SQL-kapcsolatot, és alkalmazzuk rá a migrációkat. Most nem készült és nem módosult Azure-adatbázis. Hozzáférési adat ne kerüljön a repóba.

Részletek: [helyi adatbázis](docs/local-database.md), [Saját cégek](docs/own-companies.md), [Partnerek](docs/partners.md), [adatmodell](docs/data-model.md), [Beállítások](docs/settings.md).


