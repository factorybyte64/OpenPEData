"""Generate sample C03_classic_hmi_dyn (classic WinCC Comfort/Advanced): dynamics of every screen object type.

Writes into <out_dir>:
  tags.xml          tag table C03_Tags (internal tags D_*)
  textlists/*.xml   text lists TL_Value (value/range), TL_Bit (bit 0/1), TL_BitNo (bit number)
  vbs/*.xml         VB scripts SetS1 ... SetS4 (fixed tag values of the states S1 ... S4)
  screens/*.xml     screens D_<Type>_<n>: per object type one instance per animation and per tag connection
  instances.json    screen, object, type, dynamic, tag, details
  screens.json      screen order (Next chain)

    python gen_c03.py All_Objects.xml <out_dir> <width> <height> [skip.json]

All_Objects.xml is the Openness export of the C01 screen All_Objects (default sizes of the types). skip.json lists
instances (<Type>.<Object>) TIA refused on import.
"""
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from xml.sax.saxutils import escape

HEAD = '<?xml version="1.0" encoding="utf-8"?>\n<Document>\n  <Engineering version="V21" />\n'
TAIL = '</Document>\n'

TAGS = [  # name, data type
    ('D_State', 'Int'), ('D_Bool', 'Bool'), ('D_SInt', 'SInt'), ('D_Int', 'Int'), ('D_DInt', 'DInt'),
    ('D_USInt', 'USInt'), ('D_UInt', 'UInt'), ('D_UDInt', 'UDInt'), ('D_Real', 'Real'), ('D_LReal', 'LReal'),
    ('D_String', 'WString'), ('D_DateTime', 'DateTime'),
    ('D_Pct', 'Int'), ('D_Range', 'Int'), ('D_Bits', 'Int'), ('D_Vis', 'Bool'), ('D_En', 'Bool'), ('D_X', 'Int'),
    ('D_Y', 'Int'), ('D_Max', 'Real'), ('D_Min', 'Real'), ('D_TL', 'Int'), ('D_BarR', 'Real'),
]

LONG = 'Ünïcödé äöüß ÄÖÜ € – long text that is wider than the field 0123456789 ABCDEFGHIJ'


def states(width, height):
    """Tag values of the states S1 ... S4 (VBS expressions)."""
    return {
        1: {'D_State': '1', 'D_Bool': 'False', 'D_SInt': '-128', 'D_Int': '-32768', 'D_DInt': '-2147483648',
            'D_USInt': '0', 'D_UInt': '0', 'D_UDInt': '0', 'D_Real': '-999.999', 'D_LReal': '-0.00000015',
            'D_String': '""',
            'D_DateTime': 'DateSerial(1990, 1, 1) + TimeSerial(0, 0, 0)', 'D_Pct': '0', 'D_Range': '5',
            'D_Bits': '1', 'D_Vis': 'False', 'D_En': 'False', 'D_X': '10', 'D_Y': '10', 'D_Max': '100',
            'D_Min': '0', 'D_TL': '-5', 'D_BarR': '-5'},
        2: {'D_State': '2', 'D_Bool': 'True', 'D_SInt': '12', 'D_Int': '1234', 'D_DInt': '123456', 'D_USInt': '200',
            'D_UInt': '40000', 'D_UDInt': '3000000000', 'D_Real': '3.14159', 'D_LReal': '2.718281828',
            'D_String': '"Hello World"',
            'D_DateTime': 'DateSerial(2026, 11, 22) + TimeSerial(13, 45, 30)', 'D_Pct': '50', 'D_Range': '25',
            'D_Bits': '2', 'D_Vis': 'True', 'D_En': 'True', 'D_X': str(width // 4), 'D_Y': str(height // 4),
            'D_Max': '200', 'D_Min': '-100', 'D_TL': '5', 'D_BarR': '0'},
        3: {'D_State': '3', 'D_Bool': 'True', 'D_SInt': '127', 'D_Int': '32767', 'D_DInt': '2147483647',
            'D_USInt': '255', 'D_UInt': '65535', 'D_UDInt': '4294967295', 'D_Real': '123456.789',
            'D_LReal': '1E+300',
            'D_String': '"' + LONG + '"', 'D_DateTime': 'DateSerial(2099, 12, 31) + TimeSerial(23, 59, 59)',
            'D_Pct': '100', 'D_Range': '75', 'D_Bits': '4', 'D_Vis': 'True', 'D_En': 'False',
            'D_X': str(width * 2 // 3), 'D_Y': str(height // 2), 'D_Max': '50', 'D_Min': '-50', 'D_TL': '10', 'D_BarR': '5'},
        4: {'D_State': '4', 'D_Bool': 'False', 'D_SInt': '-1', 'D_Int': '-1', 'D_DInt': '0', 'D_USInt': '1',
            'D_UInt': '1', 'D_UDInt': '1', 'D_Real': '0.0005', 'D_LReal': '-0.0', 'D_String': '"äöü ÄÖÜ ß"', 'D_DateTime': 'DateSerial(1970, 1, 1) + TimeSerial(0, 0, 0)',
            'D_Pct': '150', 'D_Range': '200', 'D_Bits': '9', 'D_Vis': 'False', 'D_En': 'True', 'D_X': '-20',
            'D_Y': '0', 'D_Max': '100', 'D_Min': '0', 'D_TL': '50', 'D_BarR': '2.5'},
    }


RANGES = [(0, 9, '255, 0, 0', '255, 255, 255', 'No'), (10, 49, '0, 160, 0', '0, 0, 0', 'No'),
          (50, 99, '0, 0, 255', '255, 255, 0', 'No')]
BITS = [(0, '255, 0, 0', '255, 255, 255'), (1, '0, 160, 0', '0, 0, 0'), (2, '0, 0, 255', '255, 255, 0'),
        (3, '255, 128, 0', '0, 0, 0')]

# tag connections (Hmi.Screen.Property + TagConnectionDynamic) accepted per type: (object, property, tag, extra attributes)
PV = {
    'IOField': [('PV_SInt', 'ProcessValue', 'D_SInt', {'FormatPattern': 's999'}), ('PV_Int', 'ProcessValue', 'D_Int', {'FormatPattern': 's99999'}),
                ('PV_DInt', 'ProcessValue', 'D_DInt', {'FormatPattern': 's9999999999'}), ('PV_USInt', 'ProcessValue', 'D_USInt', {'FormatPattern': '999'}),
                ('PV_UInt', 'ProcessValue', 'D_UInt', {'FormatPattern': '99999'}), ('PV_UDInt', 'ProcessValue', 'D_UDInt', {'FormatPattern': '9999999999'}),
                ('PV_Real', 'ProcessValue', 'D_Real', {}), ('PV_LReal', 'ProcessValue', 'D_LReal', {}),
                ('PV_String', 'ProcessValue', 'D_String', {'DataFormat': 'String'}),
                ('PV_Int_Shift2', 'ProcessValue', 'D_Int', {'ShiftDecimalPoint': '2', 'FormatPattern': 's99999'}),
                ('PV_Int_s999999', 'ProcessValue', 'D_Int', {'FormatPattern': 's999999'}),
                ('PV_Real_s9999_99', 'ProcessValue', 'D_Real', {'FormatPattern': 's9999.99'}),
                ('PV_Int_Output', 'ProcessValue', 'D_Int', {'Mode': 'Output', 'FormatPattern': 's99999'}),
                ('PV_Int_s99', 'ProcessValue', 'D_Int', {'FormatPattern': 's99'})],
    'SymbolicIOField': [('PV_TL_Value', 'ProcessValue', 'D_TL', {'@TextList': 'TL_Value'}),
                        ('PV_TL_Bit', 'ProcessValue', 'D_Bool', {'@TextList': 'TL_Bit'}),
                        ('PV_TL_BitNo', 'ProcessValue', 'D_Bits', {'@TextList': 'TL_BitNo'})],
    'DateTimeField': [('PV_DateTime', 'ProcessValue', 'D_DateTime', {})],
    'Bar': [('PV_Pct', 'ProcessValue', 'D_Pct', {}), ('PV_Real', 'ProcessValue', 'D_Real', {}),
            ('PV_BarR', 'ProcessValue', 'D_BarR', {}),
            ('PV_Pct_0_100', 'ProcessValue', 'D_Pct', {'MaximumValue': '100', 'MinimumValue': '0'}),
            ('PV_Pct_MaxMin', 'ProcessValue', 'D_Pct', {'+MaximumValue': 'D_Max', '+MinimumValue': 'D_Min'})],
    # Slider: a Real process tag is a compile error; tags on MaximumValue/MinimumValue end in an internal linker error
    'Slider': [('PV_Pct', 'ProcessValue', 'D_Pct', {}), ('PV_Int', 'ProcessValue', 'D_Int', {}),
               ('PV_Pct_0_200', 'ProcessValue', 'D_Pct', {'MaximumValue': '200', 'MinimumValue': '0'})],
    'Gauge': [('PV_Pct', 'ProcessValue', 'D_Pct', {}), ('PV_Real', 'ProcessValue', 'D_Real', {}),
              ('PV_Pct_MaxMin', 'ProcessValue', 'D_Pct', {'+MaximumValue': 'D_Max', '+MinimumValue': 'D_Min'})],
    'Switch': [('PV_Bool', 'ProcessValue', 'D_Bool', {})],
    'SymbolLibrary': [('PV_Pct', 'ProcessValue', 'D_Pct', {})],
}

MOVE_X, MOVE_Y = 40, 30


class Ids:
    def __init__(self):
        self.n = 0

    def __call__(self):
        self.n += 1
        return self.n


def trigger(ids, comp, tag):
    return (f'<Hmi.Dynamic.TagElementTrigger ID="{ids()}" CompositionName="{comp}"><LinkList><Tag TargetID="@OpenLink">'
            f'<Name>{tag}</Name></Tag></LinkList></Hmi.Dynamic.TagElementTrigger>')


def animation(ids, kind, left, top):
    """XML of one animation and a description (dynamic, tag, details)."""
    i = ids()
    if kind == 'A_Range':
        rows = ''.join(f'<Hmi.Dynamic.Range ID="{ids()}" CompositionName="RangeValues"><AttributeList><BackColor>{b}</BackColor>'
                       f'<FlashingType>{f}</FlashingType><ForeColor>{c}</ForeColor><LowerLimit>{lo}</LowerLimit>'
                       f'<UpperLimit>{hi}</UpperLimit></AttributeList></Hmi.Dynamic.Range>' for lo, hi, b, c, f in RANGES)
        return (f'<Hmi.Dynamic.RangeAppearanceAnimation ID="{i}" CompositionName="Animations"><AttributeList><Name>RangeAppearanceAnimation</Name>'
                f'</AttributeList><ObjectList>{trigger(ids, "RangeTag", "D_Range")}{rows}</ObjectList></Hmi.Dynamic.RangeAppearanceAnimation>',
                ('RangeAppearanceAnimation', 'D_Range', 'ranges ' + '; '.join(f'{lo}..{hi} back {b} fore {c}' for lo, hi, b, c, f in RANGES)))
    if kind == 'A_Flash':
        return (f'<Hmi.Dynamic.RangeAppearanceAnimation ID="{i}" CompositionName="Animations"><AttributeList><Name>RangeAppearanceAnimation</Name>'
                f'</AttributeList><ObjectList>{trigger(ids, "RangeTag", "D_Range")}<Hmi.Dynamic.Range ID="{ids()}" CompositionName="RangeValues">'
                f'<AttributeList><BackColor>255, 0, 0</BackColor><FlashingType>Fast</FlashingType><ForeColor>255, 255, 255</ForeColor>'
                f'<LowerLimit>50</LowerLimit><UpperLimit>99</UpperLimit></AttributeList></Hmi.Dynamic.Range></ObjectList>'
                f'</Hmi.Dynamic.RangeAppearanceAnimation>',
                ('RangeAppearanceAnimation', 'D_Range', 'range 50..99 back 255,0,0 fore 255,255,255 flashing Fast'))
    if kind == 'A_Bit':
        rows = ''.join(f'<Hmi.Dynamic.SingleBitPropertyValue ID="{ids()}" CompositionName="SingleBitPropertyValues"><AttributeList>'
                       f'<BackColor>{b}</BackColor><BitPosition>{p}</BitPosition><FlashingType>No</FlashingType><ForeColor>{c}</ForeColor>'
                       f'</AttributeList></Hmi.Dynamic.SingleBitPropertyValue>'
                       for p, b, c in ((0, '128, 128, 128', '255, 255, 255'), (1, '255, 255, 0', '0, 0, 0')))
        return (f'<Hmi.Dynamic.SingleBitAppearanceAnimation ID="{i}" CompositionName="Animations"><AttributeList><BitPosition>0</BitPosition>'
                f'<Name>SingleBitAppearanceAnimation</Name></AttributeList><ObjectList>{trigger(ids, "SingleBitTag", "D_Bool")}{rows}</ObjectList>'
                f'</Hmi.Dynamic.SingleBitAppearanceAnimation>',
                ('SingleBitAppearanceAnimation', 'D_Bool', 'bit 0: 0 back 128,128,128 fore 255,255,255; 1 back 255,255,0 fore 0,0,0'))
    if kind == 'A_Bits':
        rows = ''.join(f'<Hmi.Dynamic.MultipleBitsValue ID="{ids()}" CompositionName="MultipleBitsValues"><AttributeList>'
                       f'<BackColor>{b}</BackColor><BitPosition>{p}</BitPosition><FlashingType>No</FlashingType><ForeColor>{c}</ForeColor>'
                       f'</AttributeList></Hmi.Dynamic.MultipleBitsValue>' for p, b, c in BITS)
        return (f'<Hmi.Dynamic.MultipleBitsAppearanceAnimation ID="{i}" CompositionName="Animations"><AttributeList>'
                f'<Name>MultipleBitsAppearanceAnimation</Name></AttributeList><ObjectList>{trigger(ids, "MultipleBitsTag", "D_Bits")}{rows}'
                f'</ObjectList></Hmi.Dynamic.MultipleBitsAppearanceAnimation>',
                ('MultipleBitsAppearanceAnimation', 'D_Bits', '; '.join(f'bit {p} back {b} fore {c}' for p, b, c in BITS)))
    if kind == 'V_Range':
        return (f'<Hmi.Dynamic.VisibilityAnimation ID="{i}" CompositionName="Animations"><AttributeList><Name>VisibilityAnimation</Name>'
                f'<RangeEnd>49</RangeEnd><RangeStart>10</RangeStart><Visible>true</Visible></AttributeList><ObjectList>'
                f'{trigger(ids, "VisibilityTag", "D_Range")}</ObjectList></Hmi.Dynamic.VisibilityAnimation>',
                ('VisibilityAnimation', 'D_Range', 'visible for 10..49'))
    if kind == 'V_Bit':
        return (f'<Hmi.Dynamic.SingleBitVisibilityAnimation ID="{i}" CompositionName="Animations"><AttributeList><BitPosition>0</BitPosition>'
                f'<Name>VisibilityAnimation</Name><Visible>true</Visible></AttributeList><ObjectList>'
                f'{trigger(ids, "VisibilityTag", "D_Vis")}</ObjectList></Hmi.Dynamic.SingleBitVisibilityAnimation>',
                ('SingleBitVisibilityAnimation', 'D_Vis', 'visible when bit 0 is 1'))
    if kind in ('M_H', 'M_V', 'M_D'):
        name = {'M_H': 'HorizontalMovementAnimation', 'M_V': 'VerticalMovementAnimation', 'M_D': 'DiagonalMovementAnimation'}[kind]
        dx = MOVE_X if kind in ('M_H', 'M_D') else 0
        dy = MOVE_Y if kind in ('M_V', 'M_D') else 0
        ends = (f'<XPosEnd>{left + dx}</XPosEnd>' if dx else '') + (f'<YPosEnd>{top + dy}</YPosEnd>' if dy else '')
        return (f'<Hmi.Dynamic.{name} ID="{i}" CompositionName="Animations"><AttributeList><Name>{name}</Name><RangeEnd>100</RangeEnd>'
                f'<RangeStart>0</RangeStart><XDelta>{dx}</XDelta>{ends}<YDelta>{dy}</YDelta></AttributeList><ObjectList>'
                f'{trigger(ids, "MovementTag", "D_Pct")}</ObjectList></Hmi.Dynamic.{name}>',
                (name, 'D_Pct', f'range 0..100, delta x {dx} y {dy}'))
    if kind == 'M_Direct':
        return (f'<Hmi.Dynamic.DirectMovementAnimation ID="{i}" CompositionName="Animations"><AttributeList><Name>DirectMovementAnimation</Name>'
                f'</AttributeList><ObjectList>{trigger(ids, "XPosTag", "D_X")}{trigger(ids, "YPosTag", "D_Y")}</ObjectList>'
                f'</Hmi.Dynamic.DirectMovementAnimation>', ('DirectMovementAnimation', 'D_X, D_Y', 'x = D_X, y = D_Y'))
    if kind == 'E_Range':
        return (f'<Hmi.Dynamic.ObjectEnablingAnimation ID="{i}" CompositionName="Animations"><AttributeList><Name>ObjectEnablingAnimation</Name>'
                f'<ObjectEnabled>false</ObjectEnabled><RangeEnd>49</RangeEnd><RangeStart>10</RangeStart></AttributeList><ObjectList>'
                f'{trigger(ids, "ObjectEnablingTag", "D_Range")}</ObjectList></Hmi.Dynamic.ObjectEnablingAnimation>',
                ('ObjectEnablingAnimation', 'D_Range', 'disabled for 10..49'))
    if kind == 'E_Bit':
        return (f'<Hmi.Dynamic.SingleBitObjectEnablingAnimation ID="{i}" CompositionName="Animations"><AttributeList><BitPosition>0</BitPosition>'
                f'<Name>ObjectEnablingAnimation</Name><ObjectEnabled>true</ObjectEnabled></AttributeList><ObjectList>'
                f'{trigger(ids, "ObjectEnablingTag", "D_En")}</ObjectList></Hmi.Dynamic.SingleBitObjectEnablingAnimation>',
                ('SingleBitObjectEnablingAnimation', 'D_En', 'enabled when bit 0 is 1'))
    raise ValueError(kind)


ANIMS = ['A_Range', 'A_Flash', 'A_Bit', 'A_Bits', 'V_Range', 'V_Bit', 'M_H', 'M_V', 'M_D', 'M_Direct', 'E_Range', 'E_Bit']


def prop(ids, name, tag):
    return (f'<Hmi.Screen.Property ID="{ids()}" CompositionName="Properties"><AttributeList><Name>{name}</Name></AttributeList>'
            f'<ObjectList><Hmi.Dynamic.TagConnectionDynamic ID="{ids()}" CompositionName="Dynamic"><AttributeList>'
            f'<Indirect>false</Indirect></AttributeList><LinkList><Tag TargetID="@OpenLink"><Name>{tag}</Name></Tag></LinkList>'
            f'</Hmi.Dynamic.TagConnectionDynamic></ObjectList></Hmi.Screen.Property>')


def text(ids, comp, value):
    return (f'<MultilingualText ID="{ids()}" CompositionName="{comp}"><ObjectList><MultilingualTextItem ID="{ids()}" CompositionName="Items">'
            f'<AttributeList><Culture>en-US</Culture><Text>{escape("<body><p>" + escape(value) + "</p></body>")}</Text></AttributeList>'
            f'</MultilingualTextItem></ObjectList></MultilingualText>')


def item(ids, kind, name, left, top, w, h, attrs='', links='', children=''):
    if kind == 'Line':  # zero-length by default: give it a visible diagonal
        attrs += f'<EndLeft>{left + w}</EndLeft><EndTop>{top + h}</EndTop><StartLeft>{left}</StartLeft><StartTop>{top}</StartTop>'
    return (f'<Hmi.Screen.{kind} ID="{ids()}" CompositionName="ScreenItems"><AttributeList><Height>{h}</Height><Left>{left}</Left>'
            f'<ObjectName>{name}</ObjectName><Top>{top}</Top><Width>{w}</Width>{attrs}</AttributeList>'
            + (f'<LinkList>{links}</LinkList>' if links else '') + (f'<ObjectList>{children}</ObjectList>' if children else '')
            + f'</Hmi.Screen.{kind}>')


def funcs(ids, entries):
    """Click event with function list entries: ('vbs', name) or ('sys', name, [(param, kind, value)])."""
    out = ''
    for e in entries:
        if e[0] == 'vbs':
            out += (f'<Hmi.Event.FunctionListEntry ID="{ids()}" CompositionName="FunctionListEntries"><AttributeList><Name>{e[1]}</Name>'
                    f'<Type>VBScript</Type></AttributeList></Hmi.Event.FunctionListEntry>')
        else:
            ps = ''
            for pname, pkind, pval in e[2]:
                if pkind == 'link':
                    ps += (f'<Hmi.Event.FunctionListEntryParameter ID="{ids()}" CompositionName="Parameters"><AttributeList><Name>{pname}</Name>'
                           f'</AttributeList><LinkList><Value TargetID="@OpenLink"><Name>{pval}</Name></Value></LinkList>'
                           f'</Hmi.Event.FunctionListEntryParameter>')
                else:
                    ps += (f'<Hmi.Event.FunctionListEntryParameter ID="{ids()}" CompositionName="Parameters"><AttributeList><Name>{pname}</Name>'
                           f'<Value Type="System.Int32">{pval}</Value></AttributeList></Hmi.Event.FunctionListEntryParameter>')
            out += (f'<Hmi.Event.FunctionListEntry ID="{ids()}" CompositionName="FunctionListEntries"><AttributeList><Name>{e[1]}</Name>'
                    f'<Type>SystemFunction</Type></AttributeList><ObjectList>{ps}</ObjectList></Hmi.Event.FunctionListEntry>')
    return (f'<Hmi.Event.Event ID="{ids()}" CompositionName="Events"><AttributeList><Name>Click</Name></AttributeList><ObjectList>'
            f'<Hmi.Event.FunctionListEventHandler ID="{ids()}" CompositionName="EventHandler"><ObjectList>{out}</ObjectList>'
            f'</Hmi.Event.FunctionListEventHandler></ObjectList></Hmi.Event.Event>')


def button(ids, name, label, left, top, w, entries):
    return item(ids, 'Button', name, left, top, w, 22, children=funcs(ids, entries) + text(ids, 'TextOff', label) + text(ids, 'TextOn', label))


SIM = [('sys', 'SimulateTag', [('Tag', 'link', 'D_Pct'), ('Cycle', 'int', 100), ('Maximum value', 'int', 100),
                               ('Minimum value', 'int', 0), ('Value', 'int', 2)]),
       ('sys', 'SimulateTag', [('Tag', 'link', 'D_Range'), ('Cycle', 'int', 100), ('Maximum value', 'int', 99),
                               ('Minimum value', 'int', 0), ('Value', 'int', 1)])]


def bottom_bar(ids, width, height, nxt):
    y = height - 26
    out = [item(ids, 'IOField', 'State', 4, y, 60, 22, attrs='<FormatPattern>9</FormatPattern><Mode>Output</Mode>',
                children=prop(ids, 'ProcessValue', 'D_State'))]
    x = 70
    for n in range(1, 5):
        out.append(button(ids, f'S{n}', f'S{n}', x, y, 40, [('vbs', f'SetS{n}')]))
        x += 44
    # SimulateTag on a button: compile error on Comfort ("not allowed at this usage location")
    out.append(button(ids, 'Next', 'Next', width - 44, y, 40,
                      [('sys', 'ActivateScreen', [('Screen name', 'link', nxt), ('Object number', 'int', 0)])]))
    return out


def read_types(path):
    root = ET.parse(path).getroot()
    res = []
    for el in root.iter():
        if el.get('CompositionName') == 'ScreenItems':
            a = {x.tag: (x.text or '') for x in el.find('AttributeList')}
            res.append((el.tag.split('.')[-1], int(a['Width']), int(a['Height'])))
    return res


def build(types, width, height, skip):
    screens = []
    for ti, (kind, w, h) in enumerate(types):
        number = 400 + ti * 5  # stable screen numbers: 5 per type
        insts = [('Static', None, None)] + [(a, 'anim', a) for a in ANIMS] + [(p[0], 'pv', p) for p in PV.get(kind, [])]
        insts = [x for x in insts if f'{kind}.{x[0]}' not in skip]
        cw, ch = w + MOVE_X + 12, h + MOVE_Y + 22
        cols = max(1, (width - 8) // cw)
        rows = max(1, (height - 32) // ch)
        per = cols * rows
        for p in range(0, len(insts), per):
            screens.append((f'D_{kind}_{p // per + 1}', number, kind, w, h, cw, ch, cols, insts[p:p + per]))
            number += 1
    return screens


def screen_xml(scr, width, height, nxt, table):
    name, number, kind, w, h, cw, ch, cols, insts = scr
    ids = Ids()
    out = []
    for idx, (obj, mode, spec) in enumerate(insts):
        x = 6 + (idx % cols) * cw
        y = 4 + (idx // cols) * ch
        attrs, links, children, desc = '', '', '', ('Static', '', 'no dynamics')
        if mode == 'anim':
            xml, desc = animation(ids, spec, x, y)
            children = xml
        elif mode == 'pv':
            _, pname, tag, extra = spec
            children = prop(ids, pname, tag)
            dd = [f'{pname} = {tag}']
            for k, v in extra.items():
                if k.startswith('@'):
                    links += f'<{k[1:]} TargetID="@OpenLink"><Name>{v}</Name></{k[1:]}>'
                    dd.append(f'{k[1:]} {v}')
                elif k.startswith('+'):
                    children += prop(ids, k[1:], v)
                    dd.append(f'{k[1:]} = {v}')
                else:
                    attrs += f'<{k}>{escape(v)}</{k}>'
                    dd.append(f'{k} {v}')
            desc = ('TagConnection', tag, ', '.join(dd))
        out.append(item(ids, kind, obj, x, y, w, h, attrs, links, children))
        out.append(item(ids, 'TextField', f'Label_{obj}', x, y + h + MOVE_Y + 2, cw - 4, 16,
                        children=text(ids, 'Text', obj)))
        table.append({'screen': name, 'object': obj, 'type': kind, 'dynamic': desc[0], 'tag': desc[1], 'details': desc[2],
                      'left': x, 'top': y})
    out += bottom_bar(ids, width, height, nxt)
    layer = (f'    <Hmi.Screen.ScreenLayer ID="{ids()}" CompositionName="Layers"><AttributeList><Index>0</Index><Name>Layer_0</Name>'
             '</AttributeList><ObjectList>\n' + '\n'.join(out) + '\n    </ObjectList></Hmi.Screen.ScreenLayer>\n')
    return (HEAD + f'  <Hmi.Screen.Screen ID="0"><AttributeList><Name>{name}</Name><Number>{number}</Number></AttributeList>'
            '<ObjectList>\n' + layer + '  </ObjectList></Hmi.Screen.Screen>\n' + TAIL)


def tags_xml():
    rows = []
    for i, (n, t) in enumerate(TAGS):
        rows.append(f'    <Hmi.Tag.Tag ID="{i + 1}" CompositionName="Tags"><AttributeList><Name>{n}</Name></AttributeList><LinkList>'
                    f'<AcquisitionCycle TargetID="@OpenLink"><Name>1 s</Name></AcquisitionCycle><DataType TargetID="@OpenLink">'
                    f'<Name>{t}</Name></DataType><HmiDataType TargetID="@OpenLink"><Name>{t}</Name></HmiDataType></LinkList></Hmi.Tag.Tag>')
    return (HEAD + '  <Hmi.Tag.TagTable ID="0"><AttributeList><Name>C03_Tags</Name></AttributeList><ObjectList>\n'
            + '\n'.join(rows) + '\n  </ObjectList></Hmi.Tag.TagTable>\n' + TAIL)


TEXTLISTS = {
    'TL_Value': ('Decimal', [('Range', -100, -1, 'Negative (-100..-1)'), ('SingleValue', 0, 0, 'Zero'),
                             ('Range', 1, 9, 'One to nine'), ('SingleValue', 10, 10, 'Ten: Ünïcödé äöüß ÄÖÜ €'),
                             ('Default', 32000, 32000, 'Undefined value (default entry)')]),
    'TL_Bit': ('Bit', [('SingleValue', 0, 0, 'Off (0)'), ('SingleValue', 1, 1, 'On (1)')]),
    'TL_BitNo': ('Binary', [('SingleValue', 0, 0, 'Bit 0 set'), ('SingleValue', 1, 1, 'Bit 1 set'),
                         ('SingleValue', 2, 2, 'Bit 2 set'), ('SingleValue', 3, 3, 'Bit 3 set')]),
}


def textlist_xml(name, rng, entries):
    ids = Ids()
    es = ''
    for et, f, t, txt in entries:
        dflt = 'true' if et == 'Default' else 'false'
        et = 'SingleValue' if et == 'Default' else et
        es += (f'<Hmi.TextGraphicList.TextListEntry ID="{ids()}" CompositionName="Entries"><AttributeList><DefaultEntry>{dflt}</DefaultEntry>'
               f'<EntryType>{et}</EntryType><From>{f}</From><To>{t}</To></AttributeList><ObjectList>{text(ids, "Text", txt)}</ObjectList>'
               f'</Hmi.TextGraphicList.TextListEntry>')
    return (HEAD + f'  <Hmi.TextGraphicList.TextList ID="0"><AttributeList><ListRange>{rng}</ListRange><Name>{name}</Name></AttributeList>'
            f'<ObjectList>{es}</ObjectList></Hmi.TextGraphicList.TextList>\n' + TAIL)


def vbs_xml(n, values):
    body = '\n'.join(f'SmartTags("{k}") = {v}' for k, v in values.items())
    code = body  # TIA adds the Sub / End Sub frame itself
    return (HEAD + f'  <Hmi.RuntimeScripting.VBScript ID="0"><AttributeList><Code>{escape(code)}</Code><Name>SetS{n}</Name>'
            '<Type>Sub</Type></AttributeList></Hmi.RuntimeScripting.VBScript>\n' + TAIL)


def main():
    src, out, width, height = sys.argv[1], Path(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4])
    skip = set(json.loads(Path(sys.argv[5]).read_text())) if len(sys.argv) > 5 else set()
    for d in ('textlists', 'vbs', 'screens'):
        (out / d).mkdir(parents=True, exist_ok=True)
    (out / 'tags.xml').write_text(tags_xml(), encoding='utf-8')
    for name, (rng, entries) in TEXTLISTS.items():
        (out / 'textlists' / f'{name}.xml').write_text(textlist_xml(name, rng, entries), encoding='utf-8')
    st = states(width, height)
    for n, values in st.items():
        (out / 'vbs' / f'SetS{n}.xml').write_text(vbs_xml(n, values), encoding='utf-8')
    (out / 'states.json').write_text(json.dumps(st, indent=1, ensure_ascii=False), encoding='utf-8')
    screens = build(read_types(src), width, height, skip)
    table = []
    for i, scr in enumerate(screens):
        nxt = screens[(i + 1) % len(screens)][0]
        (out / 'screens' / f'{scr[0]}.xml').write_text(screen_xml(scr, width, height, nxt, table), encoding='utf-8')
    (out / 'instances.json').write_text(json.dumps(table, indent=1, ensure_ascii=False), encoding='utf-8')
    (out / 'screens.json').write_text(json.dumps([s[0] for s in screens]), encoding='utf-8')
    print(len(screens), 'screens,', len(table), 'instances')


if __name__ == '__main__':
    main()
