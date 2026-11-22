# C03b_classic_hmi_alarms_trends_recipes – classic WinCC (Comfort / RT Advanced), alarm view, trend view and recipe view with data

The control screens of C01 with content: HMI alarms in the alarm view, trends in the trend view and a recipe with data
records in the recipe view. The values come from the recipe view (data record written to the tags) in the runtime
simulation. TIA Portal V21 Update 3, WinCC Comfort/Advanced device version 17.0.0.0. Archive:
`samples/C03b_classic_hmi_alarms_trends_recipes.zap21`. It is the C06 project with the default style WinCC Dark, plus
the content described here.

## Devices

| Device | Screen size | Start screen |
|---|---|---|
| HMI_TP900 (TP900 Comfort) | 800×480 | `Ctl_01_AlarmView` |
| HMI_TP1200 (TP1200 Comfort) | 1280×800 | `Ctl_01_AlarmView` |
| HMI_PCADV (PC station, WinCC RT Advanced, `HMI_RT_1`) | 1024×768 | `Ctl_01_AlarmView` |

The content below is the same on all three devices. The tags are the internal tags of the tag table `C03_Tags` (see
C03).

## HMI alarms

Discrete alarms (trigger tag `D_Bits`, trigger bit = bit number):

| ID | Name | Alarm text | Alarm class | Trigger bit |
|---|---|---|---|---|
| 1 | Discrete alarm_1 | Alarm bit 0 (D_Bits) | Errors | 0 |
| 2 | Discrete alarm_2 | Alarm bit 1 (D_Bits) | Errors | 1 |
| 3 | Discrete alarm_3 | Alarm bit 2 (D_Bits) | Warnings | 2 |
| 4 | Discrete alarm_4 | Alarm bit 3 (D_Bits) | Warnings | 3 |

Analog alarm:

| ID | Name | Alarm text | Alarm class | Trigger tag | Limit | Limit mode |
|---|---|---|---|---|---|---|
| 1 | Analog alarm_1 | D_Range above 50 | Errors | D_Range | 50 | Higher |

## Historical data

Data log `Data_log_1` (RDB file, 500 data records, circular log) with the logging tags `Logging_tag_1` (process tag
`D_Pct`) and `Logging_tag_2` (`D_Real`), both cyclic, 1 s. Path `\Storage Card SD\Logs` on the panels, `C:\Temp` on
HMI_PCADV.

## Recipe

Recipe `Recipe_1` (number 1, path `\Flash\Recipes`, communication via tags) with five elements and two data records:

| Element | Tag | Data type | Recipe_data_record_1 | Recipe_data_record_2 |
|---|---|---|---|---|
| Recipe_element_1 | D_Int | Int | 11 | 22 |
| Recipe_element_2 | D_Real | Real | 1.5 | 2.75 |
| Recipe_element_3 | D_Pct | Int | 25 | 60 |
| Recipe_element_4 | D_Bits | Int | 9 | 2 |
| Recipe_element_5 | D_Range | Int | 75 | 25 |

## Screens

* `Ctl_01_AlarmView`: alarm view of C01, unchanged.
* `Ctl_02_TrendView`: trend view `Trend view_1` with two trends, both cyclic real time, 1 s, 100 trend values, left
  axis: `Trend_1` (`D_Pct`) and `Trend_2` (`D_Real`). On HMI_TP900 both trends are red; on HMI_TP1200 and HMI_PCADV
  `Trend_1` is black and `Trend_2` red.
* `Ctl_07_RecipeView`: recipe view `Recipe view_1` with the recipe `Recipe_1` (selection list shown).

A trend from the data log (trend type "Data log") is not configured: with such a trend the runtime simulation did not
start.

## Runtime captures

`runtime/<device>_<screen>_runtime.png`: runtime simulation (WinCC Runtime Advanced Simulator V21), screen area at
native size, captured as in C01 … C04. The screens contain the navigation template `Nav` of C01.

| Screen | HMI_TP900 | HMI_TP1200 | HMI_PCADV |
|---|---|---|---|
| `Ctl_01_AlarmView` | ✓ | ✓ | ✓ |
| `Ctl_02_TrendView` | ✓ | ✓ | ✓ |
| `Ctl_07_RecipeView` | ✓ | ✓ | ✓ |

Sequence per device, starting from the start screen with all tags 0:

1. `Ctl_07_RecipeView`: select `Recipe_data_record_2`, write the data record to the tags. `D_Bits` = 2, so alarm 2
   comes.
2. About 20 s later: select `Recipe_data_record_1` (the question whether to save changes to `Recipe_data_record_2`
   was answered with "No") and write it to the tags. `D_Bits` = 9 and `D_Range` = 75: alarms 1 and 4 and the analog
   alarm come, alarm 2 goes. Capture of `Ctl_07_RecipeView`.
3. `Ctl_01_AlarmView` (the question whether to save changes to `Recipe_data_record_1` answered with "No"): capture.
   The alarm view lists the alarms with status I (incoming) or IO (incoming and outgoing), with the time of the
   simulation run.
4. `Ctl_02_TrendView`: capture. `D_Pct` steps from 60 to 25, `D_Real` from 2.75 to 1.5.

The time and date columns and the trend time axis show the time of the simulation run (2026-11-22) and differ in every
run.

## How it was built

1. Starting point: the C06 project; default style of the project set to WinCC Dark.
2. TIA Portal UI on HMI_TP900: HMI alarms, data log, recipe with data records, trends in the trend view, recipe in the
   recipe view. Alarms, data log and recipe were copied to HMI_TP1200 and HMI_PCADV (trigger tags set again after
   pasting); the trend view and recipe view settings were entered on each device.
3. Start screens set to `Ctl_01_AlarmView`, devices compiled without errors.
4. Runtime simulation per device as described above.
