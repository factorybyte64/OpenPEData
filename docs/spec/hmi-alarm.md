# WinCC Unified: alarm classes and alarms – established, U3

Evidence: samples 107_hmi_alarmclass, 108_hmi_alarm_discrete, 109_hmi_alarm_analog (all based on 103_hmi_tag_ext; 109 additionally internal Int tag HmiTag_Int2), commit a6155fc on the user's machine; diffs `<sample>_attrdiff.txt`, truth `<sample>_truth\HMI_1\`. Multilingual alarm texts not examined (project has only en-US; project language cannot be created via TiaMcpXE).

## Alarm class – HmiAlarmClassData 0x00c01002 (package Hmi.Alarm)
- Name: `ICoreAttributes.Name`.
- `IHmiAlarmClassAttributes`: `Id_ForInstanceFilter` = Openness Id (64), AcknowledgementModel (SingleAck), AlarmAlertAcknowledgement, AlarmLogging, EmailAddress, blink flags, colors as 7-B blobs (e.g. `00 00 00 00 00 ff ff`).
- `IILHmiAlarmClassAttributes`: `Id` = sequential number (19th class), `Priority` (1; Openness reports 0), `Acknowledgement` (false), further colors.
- Links: `HmiAlarmClassDataParentRelation` → HmiDeviceData (typed slot 47 on the device); `HmiOnlyAlarmClassPeAlarmClassRelation` → **mirror object AlarmServices.AlarmClassData 0x00401003**.
- Mirror: `IAlarmClassData` Id 64, Priority 0, Acknowledgement true (contradicts IIL false), expando `AlarmServices_DisplayNameChanged`. Display name: link `AlarmClassData_IDisplayNaming_DisplayName` → HmiTextData 0x00c81005 (DefaultText = name, category AlarmTextCategory "Alarm"), `ShortName` → CoreText (empty). Text blob with one entry LCID 0xffff (neutral) and internal name "AlarmClass02".
- Predefined classes (18), e.g. "Alarm", "Warning", exist in every HMI device.

## Discrete alarm – HmiDiscreteAlarmData 0x00c01009
- Slots: ICoreAttributes, IAlarmData, IHmiAlarmAttributes, IHmiDiscreteAlarmAttributes, IHmiOnlyAlarmAttributes, IHmiScadaAlarmAttributes, IILHmiAlarmAttributes, ITextAttributes, Links.
- **Name:** `ICoreAttributes.Name` = 'Alarm_1' (`Subtype` empty, `IsShell` false). **Correction 2026-10-20 (U4):** U3 had wrongly read Subtype/IsShell; the cause was an error in `Metamodel.ancestors` (see below).
- Number: `IAlarmData.AlarmId` = Openness Id (= `SecondaryAlarmId` = `IHmiScadaAlarmAttributes.UniqueId`).
- Trigger: `IHmiDiscreteAlarmAttributes.TriggerBitNumber` (0), `TriggerMode` (OnRisingEdge), link **`IHmiDiscreteAlarmAttributes_TriggerTag`** → HmiTagData (direct, without handle/LinkMaintainer; back link `…_Inverse`).
- Class: link `HmiAlarmClassDataRelation` → HmiAlarmClassData (without back link).
- Area: `IILHmiAlarmAttributes.Area` = 'HMI_1::Alarming'.
- Device: link `HmiDiscreteAlarmDataParentRelation` → HmiDeviceData (typed slot 52).
- **Alarm text:** `ITextAttributes.AlarmText1` = 6-B expando reference `06 00 00 00 00 80`; text in its own **HmiTextData 0x00c81005** (link ExpandoParent → alarm, Category → new **AlarmTextCategory 0x00401009** with Subtype 'DiscreteAlarmText', IsFormatted, IsMultiLine). Text blob: `[u32 len][u32][ffffffff][u32 count=2]`, LCID list (0x0409, 0xffff), text as **UTF-8 plain text without HTML**, twice, with the category name ("DiscreteAlarmText") in between. Built differently from the tag comment (HTML body there).

## Analog alarm – HmiAnalogAlarmData 0x00c01006
- Like the discrete alarm: name in `Name`, AlarmId/UniqueId, Area, ITextAttributes with its own HmiTextData (Subtype 'AnalogAlarmText').
- `IHmiAnalogAlarmAttributes`: Delay, DelayTimeUnit.
- Limit: `IHmiAnalogAlarmLimitAttributes`: LimitMode ('UpperLimit'), LimitValueType ('Constant'), **LimitValueConstant = '100' as string**, Hysteresis, HysteresisMode, HysteresisInPercent, Suppression.
- Links: `IHmiAnalogAlarmAttributes_TriggerTag` → HmiTagData; `HmiAlarmClassDataRelation` → class; device typed slot 51.
- Internal Int tag: like a Bool tag but `IStructureItem.Length` 16, DataTypeRef/HmiDataType → "Int" (HMI_ILRT).

## Further
- None of the 145 HmiSystemEventData links user alarms.
- New side objects: per alarm HmiTextData +1 and AlarmTextCategory +1; per class HmiTextData +1, CoreText +1, AlarmClassData +1. No TextList entries.
- Files outside the plf: IM\HMI and RtIdFiles unchanged (confirms H-022 for alarms as well).

## Anomalies
The class priorities contradict each other (IIL 1 vs. Openness/AlarmServices 0); likewise acknowledgement (IIL false, AlarmClassData true). **Parser error (fixed in the shared parser, commit dfd18a5: `Metamodel.ancestors_topological()`; `ancestors()` unchanged):** `Metamodel.ancestors()` sorts breadth-first; with diamond-shaped inheritance the override `Comment constant` from CoreObject is applied after the one from HmiReusableConfiguration, ICoreAttributes is decoded shifted (name missing, timestamp wrong). Affected: 24 ObjectTypes of 114 (among others HmiSystemEventData, HmiInternalSystemTagData, ILScreenData, HmiDiscreteAlarmData, HmiAnalogAlarmData, HmiOnlyAlarmData, HmiUserEventData, MemberTagData, DataObjectData). Correct: sort topologically (each type before its bases, overrides from the base to the derived type).
