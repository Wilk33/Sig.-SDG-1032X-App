# SIGLENT SDG1032X Controller

Wąska aplikacja Windows do podstawowego sterowania dwukanałowym generatorem SIGLENT SDG1032X przez LAN/VXI-11.

## Najważniejsze założenia

- Okno ma 350 px szerokości i może działać obok innych aplikacji laboratoryjnych.
- CH1 i CH2 są dostępne na zakładkach, więc widoczny jest jeden kanał jednocześnie.
- Pola parametrów są ułożone pionowo.
- Każda wartość ma przyciski góra/dół z automatycznym powtarzaniem.
- Enter zatwierdza wartość wpisaną z klawiatury.
- Przyciski góra/dół wysyłają zmianę bez dodatkowego zatwierdzania.
- Nie ma przycisku Zastosuj.
- Operacje sieciowe nie blokują wątku interfejsu.
- Szybkie zmiany tego samego parametru są łączone, a do urządzenia trafia najnowsza oczekująca wartość.

## Obsługiwane ustawienia

Przebiegi:

- sinus,
- prostokąt,
- rampa,
- impuls,
- szum,
- DC.

Parametry:

- częstotliwość,
- amplituda,
- offset,
- faza,
- wypełnienie prostokąta,
- symetria rampy,
- szerokość impulsu,
- odchylenie standardowe i średnia szumu,
- poziom DC,
- obciążenie Hi-Z lub 50 Ω,
- polaryzacja normalna lub odwrócona,
- niezależny stan wyjść CH1 i CH2.

Arbitrary, modulacje, sweep, burst i pozostałe funkcje zaawansowane celowo nie są obsługiwane.

## Połączenie

Aplikacja używa VXI-11 przez LAN, tej samej metody połączenia co referencyjna aplikacja oscyloskopu.

1. Podaj adres IP generatora.
2. Kliknij przycisk Offline.
3. Po połączeniu aplikacja sprawdzi odpowiedź *IDN?.
4. Akceptowany jest model SIGLENT SDG1032X.
5. Ustawienia obu kanałów zostaną odczytane automatycznie.

## Tryb demonstracyjny

Tryb demonstracyjny nie wymaga generatora:

    dotnet run --project src/Sdg1032X.App/Sdg1032X.App.csproj -c Release -- --demo

## Budowanie i testy

Wymagany jest .NET 10 SDK dla Windows.

    dotnet run --project tests/Sdg1032X.Tests/Sdg1032X.Tests.csproj -c Release
    dotnet build SDG1032X.slnx -c Release
    dotnet publish src/Sdg1032X.App/Sdg1032X.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

## Status weryfikacji

Testy automatyczne i tryb demonstracyjny nie zmieniają stanu fizycznego urządzenia. Sterowanie prawdziwym generatorem wymaga oddzielnego testu sprzętowego opisanego w [docs/hardware-testing.md](docs/hardware-testing.md).

## Autor i licencja

- Mateusz Skipor
- Inżynier technik elektroniki
- mskiporsklep@op.pl

Projekt jest udostępniany na warunkach [PolyForm Noncommercial License 1.0.0](LICENSE).

## Dokumentacja protokołu

- [SIGLENT SDG Series Programming Guide](https://siglentna.com/wp-content/uploads/dlm_uploads/2019/12/SDG_Programming-Guide_PG02-E04A.pdf)
- [SIGLENT SDG1000X](https://www.siglent.com/in/products-overview/sdg1000x/)
