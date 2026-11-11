# Saját cégek kezelése

Az `IOwnCompanyService` Business szolgáltatás megvalósított műveletei:

- `ListAsync`: név/rövid név/adószám keresés, aktív/inaktív/összes szűrő, név/rövid név/adószám/aktív állapot szerinti kétirányú rendezés, adatbázisban végzett lapozás.
- `GetAsync`: az összes szerkeszthető adat és a `RowVersion` lekérése.
- `SaveAsync`: létrehozás és módosítás.
- `SetActiveAsync`: inaktiválás/újraaktiválás.

Minden művelet aszinkron és `Response<T>` eredményt ad. Minden hívás külön, rövid életű DbContextet használ. A kérésben átadott megszakítás továbbterjed.

## Szabályok

A név és adószám/azonosító kötelező; a rövid név és címadatok opcionálisak. A mezőhosszakat a Business réteg és a séma is védi. A kezdő/záró szóközök levágódnak, az üres opcionális mezők `null` értékre normalizálódnak. A listán a rövid név hiányában a teljes cégnév jelenik meg.

Az adószám/azonosító a saját cégek között egyedi. Mentés előtti ellenőrzés és adatbázis-index is védi: két kliens egyidejű azonos azonosítójú felvétele közül csak az egyik sikerülhet. Az egyediség a partnertörzstől független. A korábban egyeztetett belső azonosítók miatt itt nem kényszerítünk adószám-formátumot vagy ellenőrzőszámot.

Az automatikus belső azonosító külön kapcsolóval kérhető. `BELSO-` és 14 generált hexadecimális karakter alkotja, a 20 karakteres korláton belül. A már belső azonosítós cég szerkesztésekor ezt az azonosítót megőrizzük. Az egyedi index a generált azonosítókra is érvényes.

Módosításnál és állapotváltásnál szükséges a korábban betöltött verzió. Elavult verzió esetén `ConcurrencyConflict` eredmény érkezik; adat nem íródik felül. A felület a szerkesztett adatokat megtartja, a Frissítés újra lekéri a cég adatait. A duplikáció, hiányzó rekord, validációs hiba és adatbázis-elérési hiba külön eredményt kap.

Törlés nincs. Az inaktiválás nem törli és nem rendezi a korábbi kötelezettségeket. Az új kötelezettség rögzítésének aktív cégre vonatkozó ellenőrzése a későbbi Kötelezettség szolgáltatás feladata.

## WPF

A Saját cégek a kezdőképernyő. Keresés után alkalmazódnak a kereső-, szűrő- és rendezési feltételek, egy oldalon 25 cég látszik. Kijelöléskor a részletek frissen töltődnek be. Az Új cég üres űrlapot nyit. A mentés és az aktiválás külön művelet.

Nem mentett módosítások esetén a cégváltás, új űrlap, frissítés, keresés, lapozás, menüváltás és ablakbezárás előtt megerősítést kérünk. Mentés alatt a párhuzamos felületi műveleteket letiltjuk. A hat törzsadatcsoport a Beállítások menüből továbbra is elérhető.
