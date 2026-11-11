# Megmaradó helyi tesztadatbázis

- SQL Server Express LocalDB példány: `(localdb)\MSSQLLocalDB`.
- Adatbázis: `TFM_ACCOUNTING_Development`.
- Hitelesítés: az aktuális Windows-felhasználó; jelszó nem szükséges.
- A 12 üzleti/törzsadattábla mellett az EF migrációtörténet is létrejön.
- Kezdeti adatok: a partnertípusok, kötelezettségtípusok, számlatípusok, fizetési módok és rendezési típusok. Saját céget a felületen lehet felvenni.
- A felvitt adatok megmaradnak az alkalmazás leállítása és újraindítása után.

Az alkalmazás helyi alapértelmezése az `AccountingDatabase.LocalConnectionString` értéke. A `TFM_ACCOUNTING_CONNECTION_STRING` környezeti változó elsőbbséget élvez; ha korábban beállítottad, helyi használathoz töröld azt a folyamatból, vagy állítsd a helyi kapcsolatra. A felület alján látható a használt adatbázis neve és a helyi környezet jelzése. Azure-kapcsolatot most nem állítottunk be.

## Létrehozás és frissítés

```powershell
./scripts/Initialize-LocalDatabase.ps1
```

A script elindítja a LocalDB példányt, visszaállítja a rögzített EF Core CLI-eszközt és csomagokat, majd az `InitialCreate` és későbbi migrációkat a **rögzített helyi kapcsolatra** alkalmazza. A környezeti változó értékét nem használja céladatbázisként. Nincs benne adatbázis-törlés vagy újralétrehozás.

Az első migráció EF Core által generált, a modell pillanatképével együtt szerepel a Data projektben. A `docs/database-schema.sql` továbbra is generált áttekintés, nem a frissítés módja.

Ha valaki már kézzel/`EnsureCreated` használatával készített ilyen nevű, migrációtörténet nélküli adatbázist, a migrációs script hibával megállhat. Ilyenkor adatmegőrző illesztés szükséges; nem töröljük automatikusan az adatbázist.

## Megnyitás Visual Studióból

SQL Server Object Explorer → Add SQL Server → `(localdb)\MSSQLLocalDB` → Windows Authentication → Databases → `TFM_ACCOUNTING_Development`.

A LocalDB ugyanazon Windows-felhasználó helyi alkalmazáspéldányaihoz alkalmas. Két elindított desktop példánnyal a konkurenciavédelem is kipróbálható. A több számítógépes közös használatot majd az Azure SQL biztosítja.

## Automatikus ellenőrzések

A `Verify-Foundation.ps1 -Integration` véletlen nevű, `TFM_SettingsVerification_` előtagú **másik** adatbázist hoz létre, migrál és a végén töröl. Soha nem törli vagy tölti fel tesztadatokkal a megmaradó fejlesztési adatbázist. Így az általad kézzel rögzített adatok biztonságban maradnak a tesztektől.
