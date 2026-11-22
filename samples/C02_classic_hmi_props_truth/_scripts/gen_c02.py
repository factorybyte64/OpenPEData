"""Generate the property variant screens of sample C02_classic_hmi_props (classic WinCC Comfort/Advanced).

For every screen object type of C01 (basic objects and elements) one or more screens P_<Type>_<n> hold one default
instance (ObjectName "Default") and one instance per appearance attribute, each with exactly this one attribute changed
from its default (ObjectName = attribute name, a suffix _2, _3 for further values of enumerations).

Input: an Openness export of the C01 screen All_Objects (WithDefaults) - it supplies the attribute list and the default
values of every type. Output: P_*.xml files plus variants.json (screen, object, attribute, default, value) into the
output directory. Instances listed in skip.json (values TIA refused on import) are left out.

    python gen_c02.py All_Objects.xml <out_dir> <width> <height> [skip.json]
"""
import json
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from xml.sax.saxutils import escape

HEAD = '<?xml version="1.0" encoding="utf-8"?>\n<Document>\n  <Engineering version="V21" />\n'
TAIL = '</Document>\n'

# attributes that are not appearance or that are set by the layout
SKIP = {'Left', 'Top', 'Width', 'Height', 'ObjectName', 'TabIndex', 'Points', 'OcxState', 'HotKey', 'BitNumber',
        'OnValue', 'Enabled', 'UseTwoHandOperation', 'HiddenInput', 'ContinousChange', 'CompatibilityMode',
        'StartLeft', 'StartTop', 'EndLeft', 'EndTop',
        'Radius', 'RadiusHeight', 'RadiusWidth'}  # radii follow Width/Height; a changed radius ended TIA during import

# candidate values of enumerations (TIA names); the first one different from the default is used, all are tried
ENUMS = {
    'FillStyle': ['Solid', 'Transparent'],
    'BackFillStyle': ['Transparent', 'Solid'],
    'DialFillStyle': ['Transparent', 'Solid'],
    'NeedleFillStyle': ['Solid', 'Transparent'],
    'EdgeStyle': ['Dash', 'Dot', 'DashDot', 'DashDotDot', 'Null'],
    'BarEdgeStyle': ['Dash', 'Dot', 'Null'],
    'Style': ['Dash', 'Dot', 'DashDot', 'DashDotDot'],
    'StartStyle': ['Arrow', 'FilledArrow', 'Circle', 'FilledCircle', 'Line'],
    'EndStyle': ['Arrow', 'FilledArrow', 'Circle', 'FilledCircle', 'Line'],
    'LineEndShapeStyle': ['Flat', 'Square', 'Round'],
    'Flashing': ['Standard', 'None'],
    'HorizontalAlignment': ['Center', 'Right', 'Left'],
    'VerticalAlignment': ['Middle', 'Bottom', 'Top'],
    'HorizontalPictureAlignment': ['Left', 'Right', 'Center'],
    'VerticalPictureAlignment': ['Top', 'Bottom', 'Middle'],
    'TextOrientation': ['Vertical', 'VerticalLeft', 'VerticalRight', 'Horizontal'],
    'AutoSizing': ['Center', 'StretchPicture', 'AdaptObjectToPicture', 'AdaptPictureToObject'],
    'PictureAutoSizing': ['Center', 'StretchPicture'],
    'DataFormat': ['Hexadecimal', 'Binary', 'String', 'Decimal'],
    'Mode': ['Output', 'Input', 'TwoStates', 'InOutput', 'Graphic', 'TextAndGraphic', 'Invisible', 'Switch',
             'SwitchWithText', 'SwitchWithGraphic', 'Text'],
    'BarOrientation': ['Down', 'Left', 'Right', 'Up'],
    'ScalePosition': ['LeftUp', 'RightDown'],
    'SegmentColoring': ['Segmented', 'Entire'],
    'Rotation': ['Angle90', 'Angle180', 'Angle270', 'Angle0'],
    'Flip': ['Horizontal', 'Vertical', 'Both', 'None'],
    'FillColorMode': ['Shaded', 'Solid', 'Transparent', 'Original'],
    'SwitchOrientation': ['RightToLeft', 'TopToBottom', 'BottomToTop', 'LeftToRight'],
    'ScrollBarOrientation': ['Horizontal', 'Vertical'],
    'ShowScrollBar': ['Never', 'Auto', 'Permanent'],
    'TickStyle': ['Line', 'Square', 'Circle', 'Normal'],
    'NumberStyle': ['Arabic', 'Roman', 'NoNumber'],
    'CornerStyle': ['Square', 'Round'],
    'BorderInnerStyle3D': ['Raised', 'None', 'Recessed'],
    'BorderOuterStyle3D': ['Recessed', 'None', 'Raised'],
}

# C02_NAV_TOP=1: Next button at the top right and the instances one row lower (devices whose simulator window
# is taller than the desktop, so that the button is reachable without scrolling)
NAV_TOP = os.environ.get('C02_NAV_TOP') == '1'

COLORS = ['255, 0, 0', '0, 0, 255', '0, 160, 0', '255, 128, 0', '160, 0, 160', '0, 160, 160', '255, 255, 0']

NUMBERS = {  # attribute -> changed value
    'BorderWidth': '4', 'LineWidth': '5', 'CornerRadius': '12', 'FocusWidth': '4', 'BorderInnerWidth3D': '5',
    'BorderOuterWidth3D': '5', 'FieldLength': '4', 'ShiftDecimalPoint': '2', 'CountVisibleItems': '6',
    'FirstGradientOffset': '40', 'SecondGradientOffset': '40', 'MaximumValue': '200', 'MinimumValue': '-100',
    'CountSubDivisions': '2', 'IntegerDigits': '5', 'LargeTickLabelingStep': '2', 'Precision': '0',
    'ScaleGradation': '2', 'Gradation': '25', 'AngleMax': '90', 'AngleMin': '-90', 'RangeUpper1Start': '40',
    'RangeUpper2Start': '90', 'Radius': '15', 'RadiusHeight': '15', 'RadiusWidth': '30', 'RoundCornerHeight': '0',
    'RoundCornerWidth': '0', 'HourNeedleHeight': '30', 'HourNeedleWidth': '20', 'MinuteNeedleHeight': '40',
    'MinuteNeedleWidth': '16', 'SecondNeedleHeight': '40', 'SecondNeedleWidth': '6', 'TickDistance': '50',
}


def variants(attr, default):
    """Values to try for one attribute (list, first preferred)."""
    if attr in ENUMS:
        return [v for v in ENUMS[attr] if v != default]
    if default in ('true', 'false'):
        return ['false' if default == 'true' else 'true']
    if default.count(',') == 2:
        return [c for c in COLORS if c != default][:1]
    if attr in NUMBERS:
        return [NUMBERS[attr]]
    if attr.endswith('Margin'):
        return ['8']
    if attr == 'FormatPattern':
        return ['99.9']
    if attr == 'Unit':
        return ['mm']
    try:
        f = float(default)
        if '.' in default:
            return [f'{f * 0.6:.2f}']
        return [str(int(f) + 5)]
    except ValueError:
        return []


def read_types(path):
    root = ET.parse(path).getroot()
    types = []
    for el in root.iter():
        if el.get('CompositionName') == 'ScreenItems':
            attrs = [(a.tag, (a.text or '').strip()) for a in el.find('AttributeList')]
            types.append((el.tag.split('.')[-1], dict(attrs)))
    return types


def build(types, width, height, skip):
    """Returns list of screens: (name, [(object, type, w, h, attr, value)])."""
    screens = []
    number = 100
    nav_h = 26  # bottom row kept free for the navigation buttons
    for kind, attrs in types:
        w, h = int(attrs['Width']), int(attrs['Height'])
        items = [('Default', kind, w, h, None, None)]
        for attr, default in attrs.items():
            if attr in SKIP or attr.startswith('Range') and attr.endswith('Start') is False and False:
                continue
            for i, value in enumerate(variants(attr, default)):
                name = attr if i == 0 else f'{attr}_{i + 1}'
                if f'{kind}.{name}' in skip:
                    continue
                items.append((name, kind, w, h, attr, value))
        cols = max(1, (width - 10) // (w + 10))
        rows = max(1, (height - nav_h - 10) // (h + 26))
        per = cols * rows
        for p in range(0, len(items), per):
            screens.append((f'P_{kind}_{p // per + 1}', number, items[p:p + per], cols, w, h))
            number += 1
    return screens


def nav_button(target, width, height):
    """Button Next at the bottom right corner (40x22, default text): ActivateScreen with the next screen."""
    return (f'<Hmi.Screen.Button ID="99999" CompositionName="ScreenItems"><AttributeList><Height>22</Height>'
            f'<Left>{width - 42}</Left><ObjectName>Next</ObjectName><Top>{2 if NAV_TOP else height - 24}</Top><Width>40</Width>'
            f'</AttributeList><ObjectList><Hmi.Event.Event ID="99998" CompositionName="Events"><AttributeList><Name>Click</Name>'
            f'</AttributeList><ObjectList><Hmi.Event.FunctionListEventHandler ID="99997" CompositionName="EventHandler">'
            f'<ObjectList><Hmi.Event.FunctionListEntry ID="99996" CompositionName="FunctionListEntries"><AttributeList>'
            f'<Name>ActivateScreen</Name><Type>SystemFunction</Type></AttributeList><ObjectList>'
            f'<Hmi.Event.FunctionListEntryParameter ID="99995" CompositionName="Parameters"><AttributeList>'
            f'<Name>Screen name</Name></AttributeList><LinkList><Value TargetID="@OpenLink"><Name>{target}</Name></Value>'
            f'</LinkList></Hmi.Event.FunctionListEntryParameter>'
            f'<Hmi.Event.FunctionListEntryParameter ID="99994" CompositionName="Parameters"><AttributeList>'
            f'<Name>Object number</Name><Value Type="System.Int32">0</Value></AttributeList>'
            f'</Hmi.Event.FunctionListEntryParameter></ObjectList></Hmi.Event.FunctionListEntry></ObjectList>'
            f'</Hmi.Event.FunctionListEventHandler></ObjectList></Hmi.Event.Event></ObjectList></Hmi.Screen.Button>')


def screen_xml(name, number, items, cols, w, h, width, height, next_screen):
    n = 0
    out = []
    for idx, (obj, kind, iw, ih, attr, value) in enumerate(items):
        x = 10 + (idx % cols) * (w + 10)
        y = (36 if NAV_TOP else 10) + (idx // cols) * (h + 26)
        extra = f'<{attr}>{escape(value)}</{attr}>' if attr else ''
        if kind == 'Line':  # default start/end points are 0 (zero length line); give every line a visible diagonal
            extra += f'<EndLeft>{x + iw}</EndLeft><EndTop>{y + ih}</EndTop><StartLeft>{x}</StartLeft><StartTop>{y}</StartTop>'
        out.append(f'<Hmi.Screen.{kind} ID="{idx + 1}" CompositionName="ScreenItems"><AttributeList><Height>{ih}</Height>'
                   f'<Left>{x}</Left><ObjectName>{obj}</ObjectName><Top>{y}</Top><Width>{iw}</Width>{extra}'
                   f'</AttributeList></Hmi.Screen.{kind}>')
        # caption: a text field below the instance with the object name
        out.append(f'<Hmi.Screen.TextField ID="{1000 + idx}" CompositionName="ScreenItems"><AttributeList><Height>16</Height>'
                   f'<Left>{x}</Left><ObjectName>Label_{obj}</ObjectName><Top>{y + h + 2}</Top><Width>{w + 8}</Width>'
                   f'</AttributeList><ObjectList><MultilingualText ID="{5000 + idx}" CompositionName="Text"><ObjectList>'
                   f'<MultilingualTextItem ID="{9000 + idx}" CompositionName="Items"><AttributeList><Culture>en-US</Culture>'
                   f'<Text>&lt;body&gt;&lt;p&gt;{escape(obj)}&lt;/p&gt;&lt;/body&gt;</Text></AttributeList></MultilingualTextItem>'
                   f'</ObjectList></MultilingualText></ObjectList></Hmi.Screen.TextField>')
        n += 1
    out.append(nav_button(next_screen, width, height))
    layer = ('    <Hmi.Screen.ScreenLayer ID="90000" CompositionName="Layers"><AttributeList><Index>0</Index><Name>Layer_0</Name>'
             '</AttributeList><ObjectList>\n' + '\n'.join(out) + '\n    </ObjectList></Hmi.Screen.ScreenLayer>\n')
    return (HEAD + f'  <Hmi.Screen.Screen ID="0"><AttributeList><Name>{name}</Name><Number>{number}</Number></AttributeList>'
            '<ObjectList>\n' + layer
            + '  </ObjectList></Hmi.Screen.Screen>\n' + TAIL)


def main():
    src, out, width, height = sys.argv[1], Path(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4])
    skip = set(json.loads(Path(sys.argv[5]).read_text())) if len(sys.argv) > 5 else set()
    out.mkdir(parents=True, exist_ok=True)
    screens = build(read_types(src), width, height, skip)
    table = []
    for i, (name, number, items, cols, w, h) in enumerate(screens):
        nxt = screens[(i + 1) % len(screens)][0]
        (out / f'{name}.xml').write_text(screen_xml(name, number, items, cols, w, h, width, height, nxt), encoding='utf-8')
        for obj, kind, iw, ih, attr, value in items:
            table.append({'screen': name, 'object': obj, 'type': kind, 'attribute': attr, 'value': value})
    (out / 'variants.json').write_text(json.dumps(table, indent=1), encoding='utf-8')
    (out / 'screens.json').write_text(json.dumps([s[0] for s in screens]), encoding='utf-8')
    print(len(screens), 'screens,', len(table), 'instances')


if __name__ == '__main__':
    main()




