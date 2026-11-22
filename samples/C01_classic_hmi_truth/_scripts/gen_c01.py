"""Generate the Openness import files of sample C01_classic_hmi (classic WinCC Comfort/Advanced).

Writes SimaticML files for TIA Portal V21 into the directory given as argument:
  tags_<device>.xml          HMI tag table "Default tag table" (internal and external tags)
  All_Objects_<w>x<h>.xml    screen All_Objects with every basic object and element, default values
  Nav_<w>x<h>[_<skip>].xml   screen template Nav with the navigation buttons
  controls/Ctl_nn_<name>.xml empty screens for the controls, linked to the template Nav

The controls themselves are placed in the TIA Portal screen editor (see README.md of the truth folder).
"""
import sys
from pathlib import Path
from xml.sax.saxutils import escape

HEAD = '<?xml version="1.0" encoding="utf-8"?>\n<Document>\n  <Engineering version="V21" />\n'
TAIL = '</Document>\n'


class Ids:
    def __init__(self):
        self.n = 0

    def __call__(self):
        self.n += 1
        return self.n


def mlt(ids, comp, text=''):
    return (f'<MultilingualText ID="{ids()}" CompositionName="{comp}"><ObjectList>'
            f'<MultilingualTextItem ID="{ids()}" CompositionName="Items"><AttributeList>'
            f'<Culture>en-US</Culture><Text>{escape(text)}</Text></AttributeList></MultilingualTextItem>'
            f'</ObjectList></MultilingualText>')


def tag_table(connection):
    ids = Ids()
    out = [HEAD, '  <Hmi.Tag.TagTable ID="0"><AttributeList><Name>Default tag table</Name></AttributeList><ObjectList>\n']
    internal = [('Hmi_Bool', 'Bool'), ('Hmi_Int', 'Int'), ('Hmi_Real', 'Real'), ('Hmi_String', 'WString')]
    for name, dt in internal:
        out.append(f'    <Hmi.Tag.Tag ID="{ids()}" CompositionName="Tags"><AttributeList><Name>{name}</Name></AttributeList>'
                   f'<LinkList><AcquisitionCycle TargetID="@OpenLink"><Name>1 s</Name></AcquisitionCycle>'
                   f'<DataType TargetID="@OpenLink"><Name>{dt}</Name></DataType>'
                   f'<HmiDataType TargetID="@OpenLink"><Name>{dt}</Name></HmiDataType></LinkList></Hmi.Tag.Tag>\n')
    external = [('Ext_Bool', 'Plc_Bool'), ('Ext_Int', 'Plc_Int'), ('Ext_Real', 'Plc_Real'), ('Ext_Byte', 'Plc_Byte')]
    for name, plc in external:
        out.append(f'    <Hmi.Tag.Tag ID="{ids()}" CompositionName="Tags"><AttributeList><Name>{name}</Name></AttributeList>'
                   f'<LinkList><AcquisitionCycle TargetID="@OpenLink"><Name>1 s</Name></AcquisitionCycle>'
                   f'<Connection TargetID="@OpenLink"><Name>{connection}</Name></Connection>'
                   f'<ControllerTag TargetID="@OpenLink"><Name>{plc}</Name></ControllerTag></LinkList></Hmi.Tag.Tag>\n')
    out.append('  </ObjectList></Hmi.Tag.TagTable>\n')
    out.append(TAIL)
    return ''.join(out)


# (type, width, height); Circle must be square
OBJECTS = [
    ('Line', 100, 50), ('Polyline', 100, 50), ('Polygon', 100, 50), ('Ellipse', 100, 50), ('Circle', 50, 50),
    ('Rectangle', 100, 50), ('TextField', 100, 30), ('GraphicView', 100, 60),
    ('IOField', 100, 30), ('Button', 100, 40), ('SymbolicIOField', 120, 30), ('GraphicIOField', 60, 60),
    ('DateTimeField', 160, 30), ('Bar', 40, 120), ('Switch', 120, 40), ('SymbolLibrary', 60, 60),
    ('Slider', 60, 160), ('Gauge', 150, 150), ('Clock', 150, 150),
]


def screen(name, number, width, height):
    ids = Ids()
    items = []
    x, y, rowh = 10, 10, 0
    for kind, w, h in OBJECTS:
        if x + w > width - 10:
            x, y, rowh = 10, y + rowh + 20, 0
        items.append(f'        <Hmi.Screen.{kind} ID="{ids()}" CompositionName="ScreenItems"><AttributeList>'
                     f'<Height>{h}</Height><Left>{x}</Left><ObjectName>{kind}_1</ObjectName>'
                     f'<Top>{y}</Top><Width>{w}</Width></AttributeList></Hmi.Screen.{kind}>\n')
        x += w + 20
        rowh = max(rowh, h)
    layer = (f'    <Hmi.Screen.ScreenLayer ID="{ids()}" CompositionName="Layers"><AttributeList><Index>0</Index>'
             f'<Name>Layer_0</Name></AttributeList><ObjectList>\n' + ''.join(items) + '    </ObjectList></Hmi.Screen.ScreenLayer>\n')
    return (HEAD + f'  <Hmi.Screen.Screen ID="0"><AttributeList><Name>{name}</Name><Number>{number}</Number></AttributeList>'
            '<ObjectList>\n' + layer + '  </ObjectList></Hmi.Screen.Screen>\n' + TAIL)


# The 16 controls of the Controls palette (TIA Portal V21, Comfort panel / WinCC RT Advanced). Openness cannot
# create them, so each one gets an empty screen here and is placed in the screen editor afterwards.
CONTROLS = ['AlarmView', 'TrendView', 'UserView', 'HtmlBrowser', 'WatchTable', 'SmartClientView', 'RecipeView',
            'FxTrendView', 'SysDiagView', 'MediaPlayer', 'PlcCodeView', 'GraphOverview', 'ProDiagOverview',
            'CriteriaAnalysisView', 'CameraView', 'PdfView']


def control_screen_names():
    return [f'Ctl_{i + 1:02d}_{c}' for i, c in enumerate(CONTROLS)]


def nav_template(width, height, skip=()):
    """Screen template Nav: a column of small buttons at the right edge, button n activates screen Ctl_n.

    The last button activates All_Objects. The buttons keep their default text; their order is the screen order.
    Screens in skip (numbers 1..16) get no button, the other buttons keep their position."""
    ids = Ids()
    targets = control_screen_names() + ['All_Objects']
    w, h, gap = 28, 22, 4
    x = width - w - 2
    items = []
    for n, target in enumerate(targets):
        if n + 1 in skip:
            continue
        y = 2 + n * (h + gap)
        items.append(
            f'        <Hmi.Screen.Button ID="{ids()}" CompositionName="ScreenItems"><AttributeList>'
            f'<Height>{h}</Height><Left>{x}</Left><ObjectName>Nav_{n + 1:02d}</ObjectName><Top>{y}</Top><Width>{w}</Width>'
            f'</AttributeList><ObjectList>'
            f'<Hmi.Event.Event ID="{ids()}" CompositionName="Events"><AttributeList><Name>Click</Name></AttributeList><ObjectList>'
            f'<Hmi.Event.FunctionListEventHandler ID="{ids()}" CompositionName="EventHandler"><ObjectList>'
            f'<Hmi.Event.FunctionListEntry ID="{ids()}" CompositionName="FunctionListEntries"><AttributeList>'
            f'<Name>ActivateScreen</Name><Type>SystemFunction</Type></AttributeList><ObjectList>'
            f'<Hmi.Event.FunctionListEntryParameter ID="{ids()}" CompositionName="Parameters"><AttributeList>'
            f'<Name>Screen name</Name></AttributeList><LinkList><Value TargetID="@OpenLink"><Name>{target}</Name></Value>'
            f'</LinkList></Hmi.Event.FunctionListEntryParameter>'
            f'<Hmi.Event.FunctionListEntryParameter ID="{ids()}" CompositionName="Parameters"><AttributeList>'
            f'<Name>Object number</Name><Value Type=\"System.Int32\">0</Value></AttributeList></Hmi.Event.FunctionListEntryParameter>'
            f'</ObjectList></Hmi.Event.FunctionListEntry></ObjectList></Hmi.Event.FunctionListEventHandler>'
            f'</ObjectList></Hmi.Event.Event></ObjectList></Hmi.Screen.Button>\n')
    layer = (f'    <Hmi.Screen.ScreenLayer ID="{ids()}" CompositionName="Layers"><AttributeList><Index>0</Index>'
             f'<Name>Layer_0</Name></AttributeList><ObjectList>\n' + ''.join(items) + '    </ObjectList></Hmi.Screen.ScreenLayer>\n')
    return (HEAD + '  <Hmi.Screen.ScreenTemplate ID="0"><AttributeList><Name>Nav</Name></AttributeList><ObjectList>\n'
            + layer + '  </ObjectList></Hmi.Screen.ScreenTemplate>\n' + TAIL)


def control_screen(name, number):
    return (HEAD + f'  <Hmi.Screen.Screen ID="0"><AttributeList><Name>{name}</Name><Number>{number}</Number></AttributeList>'
            '<LinkList><Template TargetID="@OpenLink"><Name>Nav</Name></Template></LinkList></Hmi.Screen.Screen>\n' + TAIL)


def main():
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    for dev, conn in [('HMI_TP900', 'HMI_Connection_1'), ('HMI_PCADV', 'HMI_Connection_3'), ('HMI_TP1200', 'HMI_Connection_4')]:
        (out / f'tags_{dev}.xml').write_text(tag_table(conn), encoding='utf-8')
    for w, h in [(800, 480), (1024, 768), (1280, 800)]:
        (out / f'All_Objects_{w}x{h}.xml').write_text(screen('All_Objects', 2, w, h), encoding='utf-8')
        (out / f'Nav_{w}x{h}.xml').write_text(nav_template(w, h), encoding='utf-8')
        # f(x) trend view (8) needs a data log; the PLC diagnostic controls (11-14) end the Comfort runtime
        (out / f'Nav_{w}x{h}_no08.xml').write_text(nav_template(w, h, skip={8}), encoding='utf-8')
        (out / f'Nav_{w}x{h}_no08_11-14.xml').write_text(nav_template(w, h, skip={8, 11, 12, 13, 14}),
                                                         encoding='utf-8')
    ctl = out / 'controls'
    ctl.mkdir(exist_ok=True)
    for i, name in enumerate(control_screen_names()):
        (ctl / f'{name}.xml').write_text(control_screen(name, 10 + i), encoding='utf-8')


if __name__ == '__main__':
    main()




