# Beállítások: adatbázisban tárolt törzsadatok

Az öt domain-enum megszűnt. Mind az öt listának önálló adatbázistáblája, `DbSet`-je, idegen kulcsai és kezdeti magyar megnevezései vannak. A költségkategóriákkal együtt hat csoportként jelennek meg a WPF Beállítások felületén.

| Csoport | Kezdeti elemek |
|---|---|
| Partnertípusok | Vállalkozás, Magánszemély, Hatóság |
| Kötelezettségtípusok | Bejövő számla, Egyéb költség, Nettó bér, Járulék / adó |
| Számlatípusok | Normál, Módosító, Érvénytelenítő |
| Fizetési módok | Átutalás, Készpénz, Bankkártya, Csoportos beszedés, Egyéb |
| Rendezési típusok | Pénzmozgás, Jóváírás beszámítása |
| Költségkategóriák | Kezdetben üres; a felhasználó alakítja ki |

## Mezők

Az öt új táblában: `Id`, egyedi és állandó `Code` (50 ASCII-karakter), egyedi `Name` (100), opcionális `Notes` (2000), `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion`.

A törzsadatok minden saját cég számára közösek. Az egyedi nevek csoportonként értendők; összehasonlításuk kis- és nagybetűtől független. A `Code` belső azonosító, nem szerkeszthető mező. Új elemnél automatikusan generált; nem szükséges hozzá forráskódot módosítani vagy enumot bővíteni.

A törlés helyett inaktiválás használható. A korábbi hivatkozások megmaradnak; átnevezés után a korábbi tételek is az új nevet látják. Az új tételhez felkínált listákat `includeInactive: false` paraméterrel kell lekérni. Meglévő, inaktív elemre mutató hivatkozás megőrizhető.

## A típushoz kötődő működés

Az üzleti működés nem a felhasználó által szerkeszthető néven és nem rögzített sorszámokon alapul. A következő tulajdonságok az adatbázisban élnek:

- `ObligationType.RequiresPeriod`: kötelező-e a havi időszak.
- `ObligationType.HasInvoiceDetails`: számlaadatokat kezel-e a típus.
- `InvoiceType.RequiresOriginalInvoice`: szükséges-e eredeti számlára hivatkozni.
- `SettlementType.IsCreditOffset`: jóváírás-beszámítás vagy tényleges pénzmozgás.

Új típusnál ezek működését magyar megnevezésű választóból lehet megadni. Mentés után a működés változatlan marad; a név, megjegyzés és aktív állapot továbbra is szerkeszthető. Más működéshez új típust kell létrehozni, így egy átnevezés vagy beállításmódosítás nem változtatja meg visszamenőleg a pénzügyi tételek jelentését. Egyedi partner- és fizetési módokhoz nem kell működést választani.

Az alkalmazás nem írja vissza a kezdőértékeket induláskor; a `HasData` a kezdeti séma/migráció része. Új vagy átnevezett törzsadat nem vész el újraindításkor.

## Business és konkurenciavédelem

A `SettingsServiceFactory` csoportonként `ISettingsService` példányt ad vissza. Minden adatbázis-művelet külön, rövid életű DbContextet használ.

- `ListAsync(search, includeInactive, cancellationToken)` név szerint keres és rendez.
- `SaveAsync(request, cancellationToken)` új elemet vesz fel vagy meglévőt módosít. A kérés nem tartalmaz szabadon átírható belső kódot.
- `SetActiveAsync(id, isActive, rowVersion, cancellationToken)` aktivál/inaktivál.

Mindhárom `Response<T>`-t ad vissza. A szolgáltatás validálja a mezőket és a működésválasztást; névduplikációt és verzióütközést külön hibakóddal jelez. Az adatbázis egyedi indexe két kliens közötti versenyhelyzetben is kizárja a névduplikációt. Módosításhoz és állapotváltáshoz a betöltött `RowVersion` szükséges. A felület hiba esetén megőrzi a szerkesztett adatokat.

## Séma és további lépések

A korábbi mezők helyére kapcsolatok kerültek: `PartnerTypeId`, `ObligationTypeId`, `InvoiceTypeId`, `PaymentMethodId`, `SettlementTypeId`. A fizetési mód továbbra is opcionális a kötelezettségen, és a tényleges pénzmozgásnál kötelező. Nincsenek enum-sorszámokra korlátozó CHECK feltételek.

A korábbi típusfüggő CHECK feltételeket a táblákon belül ellenőrizhető időszak- és fizetésiszerkezet-ellenőrzések váltják. Az időszak **típus szerinti kötelezőségét**, a számlaadatok szükségességét és a rendezés típus szerinti szabályait a következő kötelezettség-/fizetési szolgáltatásokban a fenti törzsadat-tulajdonságok alapján kell ellenőrizni; ezek a szolgáltatások még nem készültek el. Közvetlen SQL vagy DbContext írásnál a típusfüggő üzleti szabályok nincsenek teljes körűen kikényszerítve.

Nem futott éles migráció. A kezdő törzsadatok az eredeti enumok sorszámait kapják, de a működés nem függ ezektől. Meglévő, korábbi sémájú adatbázisra külön adatmegőrző migrációt kell készíteni; azt a generált CREATE sémával nem szabad felülírni.
