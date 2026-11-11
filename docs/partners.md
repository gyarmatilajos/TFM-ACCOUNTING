# Partnerek kezelése

A partnertörzs a saját cégek között közös. Az `IPartnerService` listázást, részletlekérést, mentést, inaktiválást és újraaktiválást biztosít. A `ListTypesAsync` az adatbázisból adja vissza a partnertípusokat. Minden művelet aszinkron, külön DbContextet használ és `Response<T>` eredményt ad.

## Adatok és lista

- Név, adószám/azonosító és partnertípus kötelező. Rövid név, cím és legfeljebb 2000 karakteres megjegyzés opcionális.
- A kezdő/záró szóközöket levágjuk, az üres opcionális szövegeket `null`-ra normalizáljuk.
- Az adószám a partnerek között egyedi, a saját cégek adószámaitól független. Az index két egyidejű kliens mentésénél is megvédi az egyediséget.
- Külön kapcsolóval `BELSO-...` azonosító generálható. Adószám-formátumot a belső azonosítók miatt nem kényszerítünk.
- A lista névre, rövid névre és adószámra kereshető, állapotra és típusra szűrhető. Név, rövid név, adószám, típusnév és aktív állapot szerint rendezhető; a WPF felületen adatbázisban végzett, 25-ös lapozás működik.
- Új partnernél és típusváltásnál aktív típus szükséges. Meglévő partner aktuális, időközben inaktivált típusa megőrizhető. A típusszűrő a régi adatok visszakereséséhez az inaktív típusokat is megmutatja.

## Azonos név

Új partnernél és névváltoztatásnál a szolgáltatás kis- és nagybetűtől független névegyezést keres. Inaktív partnerek is részt vesznek az ellenőrzésben. Legfeljebb az első 10 találat kerül a figyelmeztetésbe. Változatlan nevű meglévő partner egyéb adatainak módosítása nem igényel újabb megerősítést.

Egyezésnél az első mentés **nem ír adatot**. A `Response<T>` eredmény nem sikeres, `RequiresConfirmation` igaz, a hibakód `ConfirmationRequired`. A másolt, csak olvasható `Warnings` gyűjteményben `PartnerNameMatch` és a kapcsolódó partnerek azonosítója, neve és adószáma szerepel.

A WPF választóablak lehetőségei:

1. **Meglévő megnyitása:** az aktuális űrlapot elveti és a kiválasztott partner friss adatait betölti. A keresőt a partner azonosítójára állítja.
2. **Mentés külön partnerként:** ugyanazt a kérést `ConfirmDuplicateName = true` értékkel megismétli.
3. **Mégse:** nem ment, az űrlap adatai megmaradnak.

A megerősített kérés is újra ellenőrzi az adószámot, típust és verziót. A névegyezés jóváhagyása nem enged duplikált adószámot vagy elavult rekordfelülírást.

## Módosítás és állapot

A `RowVersion` védi a módosítást és az aktiválást/inaktiválást. Hiba esetén a szerkesztett adatok megmaradnak. Törlés nincs; inaktiváláskor a korábbi kötelezettségek megmaradnak. Új kötelezettség aktív partnerének ellenőrzése majd a Kötelezettség szolgáltatás feladata.

Az új funkció **nem igényel migrációt**. A megmaradó helyi adatbázis és a már felvitt cégek tovább használhatók.

## Ellenőrzés

A külön eldobható LocalDB-teszt vizsgálja a mentést, névfigyelmeztetés írásmentességét, megerősítést, adószámduplikációt és versenyhelyzetet, típusokat, inaktív típus megőrzését, keresést, szűrést, rendezést, lapozást, verzióütközést, aktiválást, belső azonosítót és a kapcsolódó kötelezettség megmaradását. A WPF-ellenőrzés mindhárom figyelmeztetési választ kipróbálja és rendereli az új nézetet és választóablakot.
