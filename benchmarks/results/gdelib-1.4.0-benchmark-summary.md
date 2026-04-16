# Сводка benchmark для GDELib 1.4.0

| Scenario | Description | Mode | Avg Save, ms | StdDev Save, ms | Avg OpenAll, ms | StdDev OpenAll, ms | Avg Bytes | Avg Artifact Count |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Files_3x64KB | Три файловые ячейки по 64 КиБ | OneFile | 10.373 | 2.534 | 4.912 | 1.218 | 394102 | 4.0 |
| Files_3x64KB | Три файловые ячейки по 64 КиБ | TwoFile | 10.042 | 2.589 | 4.091 | 1.388 | 394090 | 5.0 |
| Mixed_4003 | 1000 наборов скалярных значений и три файла по 64 КиБ | OneFile | 13.759 | 4.104 | 18.409 | 4.053 | 448602 | 4.0 |
| Mixed_4003 | 1000 наборов скалярных значений и три файла по 64 КиБ | TwoFile | 16.924 | 8.388 | 22.642 | 5.218 | 448590 | 5.0 |
| Scalar_400 | 100 наборов int/double/string/bool | OneFile | 0.467 | 0.197 | 0.301 | 0.103 | 5366 | 1.0 |
| Scalar_400 | 100 наборов int/double/string/bool | TwoFile | 0.358 | 0.095 | 0.472 | 0.592 | 5354 | 2.0 |
| Scalar_4000 | 1000 наборов int/double/string/bool | OneFile | 1.473 | 0.701 | 18.190 | 4.103 | 54516 | 1.0 |
| Scalar_4000 | 1000 наборов int/double/string/bool | TwoFile | 1.197 | 0.218 | 11.339 | 0.902 | 54504 | 2.0 |
