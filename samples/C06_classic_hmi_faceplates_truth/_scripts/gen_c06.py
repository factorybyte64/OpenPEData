"""Generates the faceplate instance screen C6_Faceplates of sample C06 (classic WinCC, faceplate type FP-C05 V 1.1.0).

Usage: python gen_c06.py <out_dir> <width> <height>

The faceplate type FP-C05 (project library) has the interface properties
  FValue (Int, process value of the I/O field FP_Value), FRange (Int, range appearance of the rectangle FP_Status),
  FPct (Int, process value of the bar FP_Bar, 0..100), FVis (Bool, single bit visibility of the circle FP_Lamp),
  Text (text of FP_Title), Background_color (background color of FP_Frame).
Writes <out_dir>/screen/C6_Faceplates.xml and <out_dir>/instances.json.
"""
import json
import sys
from pathlib import Path
from xml.sax.saxutils import escape

# helpers of the C05 generator (samples/C05_classic_hmi_graphics_truth/_scripts/gen_c05.py)
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'C05_classic_hmi_graphics_truth' / '_scripts'))
from gen_c05 import Ids, bottom_bar, item, label, screen  # noqa: E402

TYPE = 'FP-C05 V 1.1.0'


def tag_prop(ids, name, tag):
    return (f'<Hmi.Screen.InterfacePropertySimple ID="{ids()}" CompositionName="Interface"><AttributeList><Name>{name}</Name>'
            f'</AttributeList><ObjectList><Hmi.Dynamic.TagConnectionDynamic ID="{ids()}" CompositionName="Dynamic"><AttributeList>'
            f'<Indirect>false</Indirect></AttributeList><LinkList><Tag TargetID="@OpenLink"><Name>{tag}</Name></Tag></LinkList>'
            f'</Hmi.Dynamic.TagConnectionDynamic></ObjectList></Hmi.Screen.InterfacePropertySimple>')


def value_prop(ids, name, value, vtype):
    return (f'<Hmi.Screen.InterfacePropertySimple ID="{ids()}" CompositionName="Interface"><AttributeList><Name>{name}</Name>'
            f'<Value Type="{vtype}">{value}</Value></AttributeList></Hmi.Screen.InterfacePropertySimple>')


def text_prop(ids, value):
    return (f'<Hmi.Screen.InterfacePropertyText ID="{ids()}" CompositionName="Interface"><AttributeList><Name>Text</Name></AttributeList>'
            f'<ObjectList><MultilingualText ID="{ids()}" CompositionName="Text"><ObjectList><MultilingualTextItem ID="{ids()}" '
            f'CompositionName="Items"><AttributeList><Culture>en-US</Culture><Text>{escape(value)}</Text></AttributeList>'
            f'</MultilingualTextItem></ObjectList></MultilingualText></ObjectList></Hmi.Screen.InterfacePropertyText>')


def hmove(ids, left, dx):
    return (f'<Hmi.Dynamic.HorizontalMovementAnimation ID="{ids()}" CompositionName="Animations"><AttributeList>'
            f'<Name>HorizontalMovementAnimation</Name><RangeEnd>100</RangeEnd><RangeStart>0</RangeStart><XDelta>{dx}</XDelta>'
            f'<XPosEnd>{left + dx}</XPosEnd><YDelta>0</YDelta></AttributeList><ObjectList>'
            f'<Hmi.Dynamic.TagElementTrigger ID="{ids()}" CompositionName="MovementTag"><LinkList><Tag TargetID="@OpenLink">'
            f'<Name>D_Pct</Name></Tag></LinkList></Hmi.Dynamic.TagElementTrigger></ObjectList></Hmi.Dynamic.HorizontalMovementAnimation>')


# name, interface tags, static values (name, value, type), text, background color, movement
INSTANCES = [
    ('FP_Dyn', {'FValue': 'D_Int', 'FRange': 'D_Range', 'FPct': 'D_Pct', 'FVis': 'D_Vis'}, [], 'Tags D_Int D_Range D_Pct', None, False),
    ('FP_Static', {}, [('FValue', 1234, 'System.Double'), ('FRange', 25, 'System.Double'), ('FPct', 75, 'System.Double')],
     'Static values', '255, 255, 200', False),
    ('FP_Alt', {'FValue': 'D_SInt', 'FRange': 'D_TL', 'FPct': 'D_UInt', 'FVis': 'D_En'}, [], 'Tags D_SInt D_TL D_UInt', '200, 230, 255', False),
    ('FP_Move', {'FValue': 'D_Int', 'FRange': 'D_Range', 'FPct': 'D_Pct', 'FVis': 'D_Vis'}, [], 'Moved by D_Pct', None, True),
]
W, H, DX = 300, 150, 40


def main():
    out, width, height = Path(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3])
    (out / 'screen').mkdir(parents=True, exist_ok=True)
    ids = Ids()
    objs, table = [], []
    for idx, (name, tags, statics, txt, back, move) in enumerate(INSTANCES):
        left = 10 + (idx % 2) * (W + 20)
        top = 10 + (idx // 2) * (H + 26)
        children = text_prop(ids, txt)
        if back:
            children += value_prop(ids, 'Background_color', back, 'System.Drawing.Color')
        for p, t in tags.items():
            children += tag_prop(ids, p, t)
        for p, v, vt in statics:
            children += value_prop(ids, p, v, vt)
        if move:
            children += hmove(ids, left, DX)
        objs.append(f'<Hmi.Screen.FaceplateInstance ID="{ids()}" CompositionName="ScreenItems"><AttributeList>'
                    f'<FaceplateTypeName>{TYPE}</FaceplateTypeName><Height>{H}</Height><Left>{left}</Left><ObjectName>{name}</ObjectName>'
                    f'<Top>{top}</Top><Width>{W}</Width></AttributeList><ObjectList>{children}</ObjectList></Hmi.Screen.FaceplateInstance>')
        objs.append(label(ids, name, left, top + H + 2, W))
        table.append({'screen': 'C6_Faceplates', 'object': name, 'type': 'FaceplateInstance', 'faceplateType': TYPE,
                      'left': left, 'top': top, 'width': W, 'height': H, 'text': txt, 'backgroundColor': back or 'type default',
                      'tags': tags, 'staticValues': {p: v for p, v, _ in statics},
                      'movement': f'HorizontalMovementAnimation D_Pct 0..100, delta x {DX}' if move else None})
    objs += bottom_bar(ids, width, height, 'C6_Faceplates')
    (out / 'screen' / 'C6_Faceplates.xml').write_text(screen('C6_Faceplates', 600, [('Layer_0', objs, True)]), encoding='utf-8')
    (out / 'instances.json').write_text(json.dumps(table, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
