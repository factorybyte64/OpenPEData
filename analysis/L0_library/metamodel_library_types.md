# L0 b) Metamodel: library/type/HMI-related types

Samples: 000_empty, 002_cpu, 004_fb, 021_mastercopy, 022_db_udt

- 000_empty: 561 types, 9 packages, XML 1024558 bytes, identical to 000_empty: True
- 002_cpu: 980 types, 15 packages, XML 1768363 bytes, identical to 000_empty: False
- 004_fb: 980 types, 15 packages, XML 1768363 bytes, identical to 000_empty: False
- 021_mastercopy: 980 types, 15 packages, XML 1768363 bytes, identical to 000_empty: False
- 022_db_udt: 980 types, 15 packages, XML 1768363 bytes, identical to 000_empty: False

## All packages (15)

0001=Siemens.Automation.ObjectFrame.Core, 0002=Siemens.Automation.DomainModel.TextLists, 0003=Siemens.Automation.CommonServices.IPI.Core, 0004=Siemens.Automation.CommonModel.ReleaseManagement, 0006=Siemens.Automation.CommonServices.Library.UserTypes, 0010=Siemens.Simatic.HwConfiguration.SecurityNg.Model, 0022=Siemens.Simatic.Lang.SiVArcSupport, 003f=Siemens.TechTrace.Editor.Model, 0040=Siemens.Simatic.AlarmServices.ObjectFrame.S7500S71200, 0041=Siemens.Simatic.SystemDiagnosis.Model.V14.Mandatory, 0042=Siemens.Simatic.Rse.Model, 0044=Siemens.Simatic.Lang.ProDiag, 0060=Siemens.Simatic.Safety, 0212=Siemens.Simatic.OpcUa.Model, 0214=Siemens.Automation.Umac

## Packages with a match in the name

- 0004 Siemens.Automation.CommonModel.ReleaseManagement
- 0006 Siemens.Automation.CommonServices.Library.UserTypes

## Types with a match (84)

| Type ID | Kind | Package | Full name | Bases | AttributeSets (impl.) | Relations |
|---|---|---|---|---|---|---|
| 00011011 | ObjectType | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.CoreLibrary | CoreDataContainer, CoreEnvironment | ICoreAttributes, ICoreLibAttributes |  |
| 00011013 | ObjectType | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.CoreLibraryObject | CoreDataContainer, CoreEnvironment | ICoreLibIdAttributes, ICoreLibObjectAttributes | InvisibleParts→CoreObject [*]; Parts→CoreObject [*]; VisibleParts→CoreObject [*] |
| 00011014 | ObjectType | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.CoreLibraryType | CoreLibraryObject, CoreTypeObject | ICoreLibTypeAttributes | DependsOn→CoreLibraryType [*]; DependsOnInverse→CoreLibraryType [*] |
| 00011015 | ObjectType | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.CoreLibraryMasterCopy | CoreLibraryObject | ICoreAttributes, ICoreLibMasterCopyAttributes |  |
| 00013014 | AttributeSet | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.ICoreLibMasterCopyAttributes |  | Attr: BinaryData, CoreObjectData, NonCoreObjectData, SharedRawData |  |
| 0001400f | Array | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.ArrayLibraryClipboardNonCoreObject |  |  |  |
| 00014010 | Array | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.ArrayLibraryClipboardCoreObject |  |  |  |
| 00015002 | Enumeration | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.CoreLibraryRootType |  | Enum: 0=GlobalLib, 1=SystemLib, 2=ProjectLib |  |
| 00015005 | Enumeration | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.LibraryClipboardObjectRole |  | Enum: 0=None, 1=SelectedObject, 2=ViewSpecificParent, 4=ExtendedObject, 8=ViewObject |  |
| 00016004 | Structure | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.LibraryClipboardNonCoreObject |  |  |  |
| 00016005 | Structure | Siemens.Automation.ObjectFrame.Core | Siemens.Automation.ObjectFrame.LibraryClipboardCoreObject |  |  |  |
| 00021014 | ObjectType | Siemens.Automation.DomainModel | Siemens.Automation.DomainModel.LibraryData | FolderParentData, CoreLibrary | ICommonProjectData, ILibraryProtectionInfo, ICoreAttributes |  |
| 00021015 | ObjectType | Siemens.Automation.CommonServices.Library.SystemTypes | Siemens.Automation.DomainModel.LibraryTypeData | SimpleFolderElementData, CoreLibraryType | ICoreAttributes | Parent→LibraryData [1] |
| 00021016 | ObjectType | Siemens.Automation.CommonServices.Library.MasterCopies | Siemens.Automation.DomainModel.LibraryMasterCopyData | DataContainerData, SimpleFolderElementData, CoreLibraryMasterCopy | IMasterCopyCustomAttributes, ICoreAttributes | Parent→LibraryData [1] |
| 00023028 | AttributeSet | Siemens.Automation.CommonServices.Library.MasterCopies | Siemens.Automation.DomainModel.IMasterCopyCustomAttributes |  | Attr:  |  |
| 00027034 | AttributeSet | Siemens.Automation.DomainModel | Siemens.Automation.DomainModel.ILibraryProtectionInfo |  | Attr: IsLibraryContentWriteProtected |  |
| 00041000 | ObjectType | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.ReleaseManagementBase | CoreObject | ICoreAttributes |  |
| 00041001 | ObjectType | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.PackageContainerDatas | ReleaseManagementBase, CoreEnvironment |  | ContainsPackage→PackageContainerData [*] |
| 00041002 | ObjectType | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.PackageContainerData | ReleaseManagementBase | IPackageContainerDataAttributes | IsRequiredBy→PackageContainerData [*]; Parent→PackageContainerDatas [1]; RelatedObject→CoreObject [*]; RelatedProduct→ProductVersionInfoData [*]; Requires→PackageContainerData [*] |
| 00041003 | ObjectType | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.ProductVersionInfoContainer | ReleaseManagementBase, CoreEnvironment |  | ContainsVersionInfo→ProductVersionInfoData [*] |
| 00041004 | ObjectType | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.ProductVersionInfoData | ReleaseManagementBase | IExtendedProductAttributes, IProductVersionInfoDataAttributes | ContainsEdition→ProductVersionInfoData [*]; IsContainedIn→ProductVersionInfoData [*]; Parent→ProductVersionInfoContainer [1]; RelatedPackageContainerData→PackageContainerData [*] |
| 00043001 | AttributeSet | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.IPackageContainerDataAttributes |  | Attr: Mandatory, Name, Version |  |
| 00043002 | AttributeSet | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.IProductVersionInfoDataAttributes |  | Attr: DisplayName, DisplayVersion, IsHidden, ProductType |  |
| 00043003 | AttributeSet | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.IExtendedProductAttributes |  | Attr:  |  |
| 00045001 | Enumeration | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.ProductType |  | Enum: 0=Product, 1=Edition, 2=OptionPackage, 3=SupportPackage, 4=ServicePack, 5=Hotfix |  |
| 00046001 | Structure | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.ProductVersion |  |  |  |
| 00047001 | BehaviourType | Siemens.Automation.CommonModel.ReleaseManagement | Siemens.Automation.CommonModel.ReleaseManagement.behavesDeepSpecialForReleaseManagement |  |  |  |
| 00061001 | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.VersionObjectBase | DataContainerData, CoreLibraryObject | IDependencyInfo, ISystemVersionOrderInfo, IVersionBaseExpandoAttributes, IVersionInfo, IVersionManufacturingInfo, ICoreAttributes | DefaultVersionToType→TypeObjectBase [1]; DependsOn→VersionObjectBase [*]; DependsOnInverse→VersionObjectBase [*]; Next→VersionObjectBase [*]; Previous→VersionObjectBase [1]; VersionObjectToTypeObject→TypeObjectBase [1] |
| 00061002 | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ByCopyVersionObject | VersionObjectBase |  |  |
| 00061003 | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ByRefVersionObject | VersionObjectBase |  |  |
| 00061005 | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.InstanceObject | CoreObject |  |  |
| 00061006 | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ByCopyInstanceObjectBase | InstanceObject |  |  |
| 00061007 | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ByRefInstanceObjectBase | InstanceObject |  |  |
| 00061008 | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.TypeObjectBase | FolderElementData | ITypeBaseAttributesInfo, ITypeBaseExpandoAttributes, ITypeInfo, ITypeProtectionInfo, ICoreAttributes | TypeObjectToVersionObject→VersionObjectBase [*]; TypeToDefaultVersion→VersionObjectBase [1] |
| 00061009 | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ByCopyTypeObjectBase | TypeObjectBase |  |  |
| 0006100a | ObjectType | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ByRefTypeObjectBase | TypeObjectBase |  |  |
| 00063001 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.IVersionInfo |  | Attr: FinalizationTime, MinimumTargetDeviceVersion, Version, VersionGuid |  |
| 00063002 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ITypeInfo |  | Attr: TypeGuid |  |
| 00063007 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ITypeProtectionInfo |  | Attr: IsWriteProtected |  |
| 00063008 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ITypeBaseAttributesInfo |  | Attr: IsObsolete, IsSetAsNonUpdateable, MinimumTargetDeviceVersion, OriginalTypeGuid, TypeNamespace |  |
| 00063009 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.IDependencyInfo |  | Attr: DependencyIdentifiers |  |
| 00063010 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ISystemVersionOrderInfo |  | Attr: SystemVersionOrderNumber |  |
| 00063011 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.IVersionManufacturingInfo |  | Attr: Manufacturer |  |
| 00063012 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.ITypeBaseExpandoAttributes |  | Attr:  |  |
| 00063013 | AttributeSet | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.IVersionBaseExpandoAttributes |  | Attr:  |  |
| 00066001 | Structure | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.VersionData |  |  |  |
| 00066002 | Structure | Siemens.Automation.CommonServices.Library.UserTypes | Siemens.Automation.CommonServices.Library.Model.DependencyIdentifierValue |  |  |  |
| 00101005 | ObjectType | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.HmiTargetData | NonProgrammableTargetData |  |  |
| 00101006 | ObjectType | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.HmiApplicationData | HmiTargetData | ICoreAttributes, IHmiApplicationAttributes |  |
| 00101007 | ObjectType | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.HmiConnectionPointData | BlockConnectionPointData | ICoreAttributes |  |
| 00103001 | AttributeSet | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.IHmiApplicationAttributes |  | Attr: AccessPoint1, AccessPoint1Master, AccessPoint2, AccessPoint2Master, AccessPointPhysical1, AccessPointPhysical2, ActivateTimeSync, AlarmEvents, BackTransfer, Computer1, Computer2, ComputerNameOrIP, DataLogFast, DataLogSlow, DeltaTransfer, DeviceApplicationType, DeviceProxy, DeviceTypeProxy, FileTargetDir, HmiRuntimeId, HttpAddress, HttpUseSSL, HttpUserName, IsRegulatedApplication, MPIAddress, MPIRouter, MPIRouting, NoExternalTimeSource, OverwritePassword, OverwriteRecipie, PathToTargetOS, PreferredServer, RedundantPartners, RegularlyMinutes, RuntimeCompilerFamily, SendOnce, SerialBaudRate, SerialPort, SymbolicName, SymbolicNameOfAccessPoints, TimeDisplay, TimeFormatting, TransferMode, TransferNames, UseDeviceAddress, ViaSystemBus, ViaTerminalBus |  |
| 00105001 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.HmiApplicationRtId |  | Enum: 0=HmiRt, 1=HmiScadaRt, 2=HmiScadaRtStandby, 3=HmiClient, 4=HmiCas, 5=HmiConnectivity, 6=HmiSl, 7=HmiProSl, 8=HmiILRT |  |
| 00105002 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.PreferredServerType |  | Enum: 0=None, 1=Server, 2=Standby |  |
| 00105003 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.TransferModeEnum |  | Enum: 0=Ethernet, 1=MpiDp, 2=File, 3=Http, 4=Serial, 5=SerialPpi, 6=Usb, 7=UsbPpi, 8=S7Ethernet |  |
| 00105004 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.DeltaTransferModeEnum |  | Enum: 0=On, 1=Off |  |
| 00105005 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.SerialBaudRateEnum |  | Enum: 0=Rate_9600, 1=Rate_19200, 2=Rate_38400, 3=Rate_57600, 4=Rate_115200 |  |
| 00105006 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.NoExternalTimeSourceEnum |  | Enum: 0=NoSource, 1=InternalRecieveUtility, 2=ExternalRecieveUtility |  |
| 00105007 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.SendOnceEnum |  | Enum: 0=SendOnce, 1=Regularly |  |
| 00105008 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.AccessPoint1Enum |  | Enum: 0=Cp1613_1, 1=Cp1613_2, 2=Softnet_1, 3=Softnet_2 |  |
| 00105009 | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.AccessPoint2Enum |  | Enum: 0=None, 1=Cp1613_1, 2=Cp1613_2, 3=Softnet_1, 4=Softnet_2 |  |
| 0010500a | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.AccessPointMasterEnum |  | Enum: 0=Master, 1=Slave |  |
| 0010500b | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.TimeDisplayEnum |  | Enum: 0=ServerTimeZone, 1=LocalTimeZone, 2=UtcTimeZone |  |
| 0010500c | Enumeration | Siemens.Simatic.HwConfiguration.Model | Siemens.Simatic.HwConfiguration.Hmi.Model.TimeFormattingEnum |  | Enum: 0=Individual, 1=CommonIso_8601 |  |
| 00221019 | ObjectType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Blocks.IECPLLibraryTypeData | LibraryTypeData | ICoreAttributes |  |
| 0022101c | ObjectType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Blocks.IECPLLibraryTargetBlockData | FunctionalObjectData | ICoreAttributes, IGenericInterfaceData | DependsOn→IECPLLibraryTargetBlockData [*]; DependsOnInverse→IECPLLibraryTargetBlockData [*] |
| 0022101d | ObjectType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Blocks.IECPLLibraryTargetData | ControllerTargetData, FolderParentData, CoreTarget | ICoreAttributes |  |
| 00221608 | ObjectType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.LibraryBlockInterfaceRootContainer | CoreEnvironment | ICoreAttributes | BlockInterfaceContainer→LibraryBlockInterfaceContainer [*] |
| 00221609 | ObjectType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.LibraryBlockInterfaceContainer | BlockInterfaceContainerBase | ICoreAttributes | Parent→LibraryBlockInterfaceRootContainer [1] |
| 0022160c | ObjectType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.InterfaceVersionRootData | InterfacePartData | ICoreAttributes, IInterfaceLibraryData, IInterfaceVersionData | UsedByBlock→BlockInterfaceBaseData [1]; UsedByIdentManager→IdentContainerData [*]; UsedByLibraryBlocks→BlockInterfaceBaseData [*]; UsedBySnapshotData→SnapshotData [*]; UsedParts→InterfacePartData [*]; UsedTypeLibElements→BlockInterfaceBaseData [*]; VersionElementToBlock→BlockInterfaceBaseData [1] |
| 00223601 | AttributeSet | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.IInterfaceVersionData |  | Attr: BlockName, BlockRID, Context, ErrorStore, HasNamespace, HighestIdentUId, InterfaceModified, MemberDeletedChangeCounter, OMSpTypeModified, OffsetModified, ParameterModified, SetPointCount, SourceObjectID, State, StructureModified, SubTypeVersionChangeCount, TComSize, TrackingGuid, TrackingHistory, Version, VolatileAreaSize |  |
| 00223602 | AttributeSet | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.IInterfaceLibraryData |  | Attr: Compiler, Flags, PartName, PartTypeId, TemplateValues, Version, VersionId |  |
| 00224001 | Array | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Blocks.ArrayOfPLVersions |  |  |  |
| 00225601 | Enumeration | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.InterfacePartVersion |  | Enum: 0=Undef, 1=V13_01, 2=V14_00, 3=V14_01, 5=V15_00, 6=V15_01, 7=V16_00, 9=V17_00, 10=V18_00, 12=V18_01, 14=V19_00, 16=V20_00 |  |
| 00225605 | Enumeration | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.InterfaceLibraryFlag |  | Enum: 0=None, 1=IsFBT, 2=UserHidden, 4=InstrGuid |  |
| 00226001 | Structure | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Blocks.PLVersion |  |  |  |
| 00227601 | BehaviourType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.usesBlockInterfaceVersion |  |  |  |
| 00227603 | BehaviourType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.usesLibraryInterfaceVersion |  |  |  |
| 00227607 | BehaviourType | Siemens.Simatic.Lang | Siemens.Simatic.Lang.Model.Structures.referenceOldBlockInterfaceVersion |  |  |  |
| 00401011 | ObjectType | Siemens.Simatic.AlarmServices.ObjectFrame.S71500S71200.V12 | Siemens.Simatic.AlarmServices.ObjectFrame.Private.TypeInstanceConnectorData | CoreObject | ICoreAttributes | TheInstanceAlarms→ControllerAlarmData [*]; TheTypeAlarm→ControllerAlarmData [1] |
| 00411003 | ObjectType | Siemens.Simatic.SystemDiagnosis.Model | Siemens.Simatic.SystemDiagnosis.Model.TextlistLibraryTypeData | LibraryTypeData | ICoreAttributes |  |
| 00601a09 | AttributeSet | Siemens.Simatic.Safety | Siemens.Simatic.Safety.Model.IFailsafeVersion |  | Attr: FailsafeVersions |  |
| 00603a0d | Structure | Siemens.Simatic.Safety | Siemens.Simatic.Safety.Model.FailsafeVersionElement |  |  |  |
| 00603a0e | Array | Siemens.Simatic.Safety | Siemens.Simatic.Safety.Model.ArrayOfFailsafeVersions |  |  |  |
| 00603a3a | AttributeSet | Siemens.Simatic.Safety | Siemens.Simatic.Safety.Model.ILibraryBlockInfo |  | Attr: IsTypeLibElementWithCoded |  |

## Objects of these types per sample

- 000_empty: LibraryData×1, PackageContainerDatas×1, PackageContainerData×25, ProductVersionInfoContainer×1, ProductVersionInfoData×6
- 002_cpu: LibraryData×1, PackageContainerDatas×1, PackageContainerData×39, ProductVersionInfoContainer×1, ProductVersionInfoData×30, LibraryBlockInterfaceRootContainer×1, LibraryBlockInterfaceContainer×1, InterfaceVersionRootData×1
- 004_fb: LibraryData×1, PackageContainerDatas×1, PackageContainerData×39, ProductVersionInfoContainer×1, ProductVersionInfoData×30, LibraryBlockInterfaceRootContainer×1, LibraryBlockInterfaceContainer×1, InterfaceVersionRootData×2
- 021_mastercopy: LibraryData×1, LibraryMasterCopyData×1, PackageContainerDatas×1, PackageContainerData×39, ProductVersionInfoContainer×1, ProductVersionInfoData×30, LibraryBlockInterfaceRootContainer×1, LibraryBlockInterfaceContainer×1, InterfaceVersionRootData×3
- 022_db_udt: LibraryData×1, PackageContainerDatas×1, PackageContainerData×39, ProductVersionInfoContainer×1, ProductVersionInfoData×30, LibraryBlockInterfaceRootContainer×1, LibraryBlockInterfaceContainer×1, InterfaceVersionRootData×7
