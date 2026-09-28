# Test sprzętowy SIGLENT SDG1032X

## Zasady

- Zacznij z odłączonym obciążeniem.
- Pozostaw oba wyjścia wyłączone podczas pierwszego odczytu.
- Zweryfikuj adres IP na panelu generatora.
- Sprawdź, czy aplikacja pokazuje dokładnie model SDG1032X.
- Wartości graniczne sprawdzaj zgodnie z instrukcją urządzenia i aktualnym obciążeniem.

## Procedura

1. Połącz aplikację z generatorem przez LAN.
2. Odczytaj CH1 i CH2 i porównaj wartości z panelem urządzenia.
3. Dla każdego podstawowego przebiegu ustaw bezpieczną częstotliwość, amplitudę i offset.
4. Zatwierdź wartość Enterem i sprawdź panel generatora.
5. Użyj pojedynczego kliknięcia strzałki oraz przytrzymania strzałki.
6. Sprawdź, czy UI pozostaje responsywny, a urządzenie kończy na najnowszej wartości.
7. Sprawdź wypełnienie, symetrię, szerokość impulsu, szum i poziom DC.
8. Sprawdź Hi-Z, 50 Ω oraz obie polaryzacje.
9. Włącz i wyłącz każde wyjście osobno.
10. Po ponownym odczycie porównaj wszystkie widoczne wartości z panelem generatora.

## Raport

Zapisz model, wersję firmware, użyte połączenie, wynik każdego kroku oraz wszystkie różnice względem panelu urządzenia. Test programu bez generatora nie jest potwierdzeniem działania zapisu sprzętowego.

## Walidacja wersji 0.1.2

28 września 2026 r. wykonano ograniczony test na rzeczywistym generatorze:

- urządzenie zostało poprawnie rozpoznane jako SIGLENT SDG1032X z firmware 1.01.01.33R8,
- aplikacja automatycznie odczytała ustawienia i stan obu kanałów po połączeniu,
- oba wyjścia były wyłączone przed testem i pozostały wyłączone po teście,
- CH1 przyjął tymczasową zmianę częstotliwości z 3000 Hz na 3001 Hz,
- CH2 przyjął tymczasową zmianę częstotliwości z 2000 Hz na 2001 Hz,
- pierwotne częstotliwości 3000 Hz i 2000 Hz zostały przywrócone i potwierdzone ponownym odczytem.

Test nie obejmował włączania wyjść, zmiany typu przebiegu, amplitudy, offsetu, obciążenia ani polaryzacji.
