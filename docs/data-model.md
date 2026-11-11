# Elfogadott adatmodell és megvalósítási határok

## Hét üzleti entitás és öt új törzsadattábla

Az öt korábbi enum adatbázisban tárolt törzsadattá alakult: **PartnerType, ObligationType, InvoiceType, PaymentMethod, SettlementType**. Ezek a költségkategóriákkal együtt a Beállítások alatt kezelhetők. A teljes modell **12 entitásból** áll. A típusmezők idegen kulcsokra (`...Id`) és navigációs tulajdonságokra cserélődtek. Részletek és az új típusok működése: [Beállítások](settings.md).

1. **OwnCompany:** név (200), opcionális rövid név (50), kötelező egyedi adószám/azonosító (20), opcionális irányítószám (10), város (100), cím (200), megjegyzés (2000), aktív állapot.
2. **Partner:** a saját céghez hasonló adatok, továbbá vállalkozás/magánszemély/hatóság típus. Közös törzs a saját cégek között. Az adószám egyedisége a saját cégektől független.
3. **Obligation:** saját cég, partner, típus, opcionális kategória, leírás (200), vonatkozási év/hónap, határidő, előjeles HUF-összeg, tervezett fizetési mód, opcionális eredeti kötelezettség, megjegyzés és archiválás. Bérnél és járuléknál kötelező az időszak. A többi típusnál az év/hónap együtt opcionális. Nulla összeg nem megengedett.
4. **InvoiceDetails:** kötelezettséghez tartozó egy-egy kapcsolat, számlaszám (100), normál/módosító/érvénytelenítő típus, kiállítás és teljesítés dátuma, nettó és áfa. A bruttó a kötelezettségen található.
5. **CostCategory:** közös, egyszintű kategória, egyedi név (100), megjegyzés és aktív állapot.
6. **PaymentEntry:** kötelezettség, pénzmozgás/beszámítás típus, dátum, előjeles összeg, tényleges fizetési mód, hivatkozás (200), beszámítási csoportazonosító és megjegyzés.
7. **Attachment:** kötelezettség, eredeti fájlnév (255), egyedi relatív NAS-út (1000), MIME-típus (100), méret és megjegyzés. Támogatott: PDF, JPG, JPEG, PNG, DOC, DOCX, XLS, XLSX, CSV.

Minden entitáson `RowVersion` védi a rekord módosítását/törlését. A számlaadatok kivételével automatikus létrehozási és módosítási UTC-időbélyeg található a rekordokon. A mezők részletes típusait és korlátait az entitások és az EF konfiguráció tartalmazzák.

## Adatbázisban már érvényesülő szabályok

- Egyedi, kötelező adószámok, kis- és nagybetűtől független összehasonlítás.
- Egyedi kategórianév és relatív csatolmányút.
- Saját cég + partner + számlaszám egyediség. Ehhez az InvoiceDetails két technikai mezőt is tartalmaz (`OwnCompanyId`, `PartnerId`); összetett idegen kulcs garantálja, hogy megegyeznek a szülőével. Ezek nem külön felületi mezők. A szülő ezen kulcsmezői EF-ben nem módosíthatók közvetlenül; a későbbi javító szolgáltatás a számlaadatokat tranzakcióban újrakapcsolja.
- Nincs kaszkádos törlés. Használt törzsadatokat a Business réteg inaktivál majd.
- Pontos `decimal(18,2)` összegek, `date` üzleti dátumok, `rowversion` konkurenciavédelem.
- Nem üres nevek/azonosítók, törzsadathivatkozások, időszakpárok és nem nulla összegek ellenőrzése.
- A rendezés szerkezete szerint vagy fizetési mód, vagy beszámítási csoport adható meg. A rendezési típus tulajdonságaival való egyezést a későbbi Business szolgáltatás ellenőrzi.

A generált NAS-út csak ASCII-azonosítókat tartalmazzon: a `varchar(1000)` mező így egyedi indexként is elfér. Az eredeti fájlnév Unicode marad. A NAS-gyökér közös konfiguráció, nem része a tárolt relatív útnak.

## A Business műveletekben még megvalósítandó szabályok

- Automatikus, egyedi `BELSO-...` azonosító ismeretlen adószám esetén; azonos partnernévnél figyelmeztetés.
- Kötelező számlaadat számla típusnál, más típusoknál tiltott számlaadat. Nettó + áfa = kötelezettség összege. Módosításnál/érvénytelenítésnél kötelező eredeti, azonos céghez és partnerhez tartozó számla; hivatkozási körök tiltása.
- Hátralék = kötelezettség összege mínusz rendezési tételek összege. Részfizetés és részleges beszámítás megengedett, túlrendezés nem. Az archivált nyitott tételek is beleszámítanak a tartozásba.
- A beszámítás két, azonos csoportú, ellentétes összegű tétel, azonos saját céggel és partnerrel. Mindkét oldal egy tranzakcióban mentendő, módosítandó és törlendő. A pénzmozgás-kimutatásokból kizárandó.
- A `rowversion` önmagában nem védi két új fizetési tétel egyidejű felvételét. A fizetési szolgáltatásnak a kötelezettséget is védenie kell, és tranzakción belül újra ellenőriznie kell a hátralékot.
- Törzsadatok üres, inaktív vagy nem létező értékeinek ellenőrzése; üzleti hibák, duplikációk és konkurenciahibák felhasználóbarát `Response<T>` eredménye.
- NAS: biztonságos relatív út, egyedi fájlnév, másolás és hibakompenzáció; sikeres csatolmány csak sikeres másolás után. Fájlcsere új csatolmányként. Az adatbázis és a fájlrendszer nem közös tranzakció.

Ezek több rekordot vagy külső tárolót érintő szabályok; a kötelezettség-, fizetés- és csatolmány-szolgáltatások még nem készültek el. A Saját cégek, Partnerek és a hat beállításcsoport Business szolgáltatása és WPF felülete már elkészült. A saját cégek és partnerek belső azonosítója generálható, partnernév-egyezésnél megerősíthető figyelmeztetés működik. A típusfüggő szabályokhoz az új törzsadattáblák működésjelzőit kell használni.

## Response<T>

Csak a `Success`, `Failure`, `Invalid` és `ConfirmationRequired` gyártómetódusokkal példányosítható. A validációs hibák és figyelmeztetések másolt, csak olvasható gyűjteményben szerepelnek. Eredmény nélküli művelethez `Response<Unit>` használható. Váratlan hibák naplózása és a `DbUpdateConcurrencyException`/egyediségsértések fordítása a szolgáltatások feladata lesz. Lemondott műveletnél a `CancellationToken` szerinti megszakítás továbbterjedjen.

## Hivatkozások

- [Microsoft: SQL Server és Azure SQL EF Core provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/)
- [Microsoft: konkurenciaütközések kezelése](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)

