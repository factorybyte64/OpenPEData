"""Generate sample C05_classic_hmi_graphics (classic WinCC Comfort/Advanced): graphics, text and graphic lists,
screen template, groups, layers, pop-up and slide-in screens.

Requires the content of C03 in the device (tag table C03_Tags, text lists TL_*, VB scripts SetS1 ... SetS4) and the
project graphics G_* (imported from _scripts/img). Writes into <out_dir>:
  graphiclists/*.xml   GL_Value (value/range), GL_Bit (bit), GL_BitNo (bit number)
  template/T_C05.xml   screen template with a header
  popup/Popup_C05.xml  pop-up screen
  slidein/Top.xml      slide-in screen Top
  screens/*.xml        screens C5_*
  instances.json       screen, object, type, configuration
  screens.json         screen order (Next chain)

    python gen_c05.py <out_dir> <width> <height> [rt]
"""
import json
import sys
from pathlib import Path
from xml.sax.saxutils import escape

HEAD = '<?xml version="1.0" encoding="utf-8"?>\n<Document>\n  <Engineering version="V21" />\n'
TAIL = '</Document>\n'

GRAPHICS = ['G_PNG', 'G_JPG', 'G_BMP', 'G_GIF', 'G_TIF', 'G_EMF', 'G_WMF', 'G_SVG', 'G_TALL', 'G_WIDE']

GRAPHICLISTS = {
    'GL_Value': ('Decimal', [('Range', -100, -1, 'G_GIF'), ('SingleValue', 0, 0, 'G_PNG'), ('Range', 1, 9, 'G_JPG'),
                             ('SingleValue', 10, 10, 'G_BMP'), ('Default', 32000, 32000, 'G_SVG')]),
    'GL_Bit': ('Bit', [('SingleValue', 0, 0, 'G_EMF'), ('SingleValue', 1, 1, 'G_WMF')]),
    'GL_BitNo': ('Binary', [('SingleValue', 0, 0, 'G_PNG'), ('SingleValue', 1, 1, 'G_JPG'), ('SingleValue', 2, 2, 'G_BMP'),
                            ('SingleValue', 3, 3, 'G_TIF')]),
}


class Ids:
    def __init__(self):
        self.n = 0

    def __call__(self):
        self.n += 1
        return self.n


def text(ids, comp, value):
    return (f'<MultilingualText ID="{ids()}" CompositionName="{comp}"><ObjectList><MultilingualTextItem ID="{ids()}" CompositionName="Items">'
            f'<AttributeList><Culture>en-US</Culture><Text>{escape("<body><p>" + escape(value) + "</p></body>")}</Text></AttributeList>'
            f'</MultilingualTextItem></ObjectList></MultilingualText>')


def prop(ids, name, tag):
    return (f'<Hmi.Screen.Property ID="{ids()}" CompositionName="Properties"><AttributeList><Name>{name}</Name></AttributeList>'
            f'<ObjectList><Hmi.Dynamic.TagConnectionDynamic ID="{ids()}" CompositionName="Dynamic"><AttributeList>'
            f'<Indirect>false</Indirect></AttributeList><LinkList><Tag TargetID="@OpenLink"><Name>{tag}</Name></Tag></LinkList>'
            f'</Hmi.Dynamic.TagConnectionDynamic></ObjectList></Hmi.Screen.Property>')


def item(ids, kind, name, left, top, w, h, attrs='', links='', children=''):
    return (f'<Hmi.Screen.{kind} ID="{ids()}" CompositionName="ScreenItems"><AttributeList><Height>{h}</Height><Left>{left}</Left>'
            f'<ObjectName>{name}</ObjectName><Top>{top}</Top><Width>{w}</Width>{attrs}</AttributeList>'
            + (f'<LinkList>{links}</LinkList>' if links else '') + (f'<ObjectList>{children}</ObjectList>' if children else '')
            + f'</Hmi.Screen.{kind}>')


def link(role, name):
    return f'<{role} TargetID="@OpenLink"><Name>{name}</Name></{role}>'


def label(ids, obj, x, y, w):
    return item(ids, 'TextField', f'Label_{obj}', x, y, w, 16, children=text(ids, 'Text', obj))


def funcs(ids, entries):
    out = ''
    for e in entries:
        if e[0] == 'vbs':
            out += (f'<Hmi.Event.FunctionListEntry ID="{ids()}" CompositionName="FunctionListEntries"><AttributeList><Name>{e[1]}</Name>'
                    f'<Type>VBScript</Type></AttributeList></Hmi.Event.FunctionListEntry>')
            continue
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


def button(ids, name, caption, left, top, w, entries):
    return item(ids, 'Button', name, left, top, w, 22,
                children=funcs(ids, entries) + text(ids, 'TextOff', caption) + text(ids, 'TextOn', caption))


POPUP = ('sys', 'ShowPopupScreen', [('Name of the screen', 'link', 'Popup_C05'), ('X coordinate', 'int', 200),
                                     ('Y coordinate', 'int', 120), ('Display mode', 'int', -1), ('Animation', 'int', -1),
                                     ('Animation speed', 'int', 1)])  # display mode -1 = toggle, animation -1 = off
SLIDEIN = ('vbs', 'C5_ToggleSlideIn')  # VB script: ShowSlideInScreen of the slide-in screen Top


def bottom_bar(ids, width, height, nxt, extra=()):
    y = height - 26
    out = [item(ids, 'IOField', 'State', 4, y, 60, 22, attrs='<FormatPattern>9</FormatPattern><Mode>Output</Mode>',
                children=prop(ids, 'ProcessValue', 'D_State'))]
    x = 70
    for n in range(1, 5):
        out.append(button(ids, f'S{n}', f'S{n}', x, y, 40, [('vbs', f'SetS{n}')]))
        x += 44
    for name, cap, entry in extra:
        out.append(button(ids, name, cap, x, y, 60, [entry]))
        x += 64
    out.append(button(ids, 'Next', 'Next', width - 44, y, 40,
                      [('sys', 'ActivateScreen', [('Screen name', 'link', nxt), ('Object number', 'int', 0)])]))
    return out


def screen(name, number, layers, template=None):
    lay = ''
    for idx, (lname, items, visible) in enumerate(layers):
        vis = '' if visible else '<VisibleES>false</VisibleES>'
        lay += (f'    <Hmi.Screen.ScreenLayer ID="{90000 + idx}" CompositionName="Layers"><AttributeList><Index>{idx}</Index>'
                f'<Name>{lname}</Name>{vis}</AttributeList><ObjectList>\n' + '\n'.join(items) + '\n    </ObjectList></Hmi.Screen.ScreenLayer>\n')
    tl = f'<LinkList>{link("Template", template)}</LinkList>' if template else ''
    return (HEAD + f'  <Hmi.Screen.Screen ID="0"><AttributeList><Name>{name}</Name><Number>{number}</Number></AttributeList>{tl}'
            '<ObjectList>\n' + lay + '  </ObjectList></Hmi.Screen.Screen>\n' + TAIL)


def grid(width, height, cw, ch, top=40):
    cols = max(1, (width - 8) // cw)
    rows = max(1, (height - top - 32) // ch)
    return cols, rows


def build(width, height):
    """Returns [(screen name, [(object, type, w, h, attrs, links, children-factory, description)])]."""
    pages = []
    # graphic views: every format at the image size (120x80), no stretching
    gv = [(f'GV_{g[2:]}', 'GraphicView', 120, 80, '<FitToLargest>false</FitToLargest>', link('Picture', g), None,
           f'graphic {g}, 120x80, FitToLargest false') for g in GRAPHICS]
    pages.append(('C5_GraphicView_1', gv))
    # stretch / size variants
    sv = []
    for g in ('G_PNG', 'G_TALL', 'G_WIDE'):
        for w, h in ((160, 60), (60, 120), (80, 80)):
            for fit in ('false', 'true'):
                sv.append((f'{g[2:]}_{w}x{h}_Fit{fit[0].upper()}', 'GraphicView', w, h, f'<FitToLargest>{fit}</FitToLargest>',
                           link('Picture', g), None, f'graphic {g}, object {w}x{h}, FitToLargest {fit}'))
    sv.append(('BMP_Transparent', 'GraphicView', 120, 80,
               '<FitToLargest>false</FitToLargest><TransparentColor>255, 255, 240</TransparentColor><UseTransparentColor>true</UseTransparentColor>',
               link('Picture', 'G_BMP'), None, 'graphic G_BMP, transparent color 255,255,240'))
    sv.append(('PNG_BackTransparent', 'GraphicView', 120, 80, '<BackFillStyle>Transparent</BackFillStyle><FitToLargest>false</FitToLargest>',
               link('Picture', 'G_PNG'), None, 'graphic G_PNG (alpha), back fill transparent'))
    pages.append(('C5_GraphicView_2', sv))
    # lists
    li = [
        ('SIO_TL_Value', 'SymbolicIOField', 160, 30, '', link('TextList', 'TL_Value'), ('ProcessValue', 'D_TL'), 'text list TL_Value, tag D_TL'),
        ('SIO_TL_Bit', 'SymbolicIOField', 160, 30, '', link('TextList', 'TL_Bit'), ('ProcessValue', 'D_Bool'), 'text list TL_Bit, tag D_Bool'),
        ('SIO_TL_BitNo', 'SymbolicIOField', 160, 30, '', link('TextList', 'TL_BitNo'), ('ProcessValue', 'D_Bits'), 'text list TL_BitNo, tag D_Bits'),
        ('SIO_TL_Value_Out', 'SymbolicIOField', 160, 30, '<Mode>Output</Mode>', link('TextList', 'TL_Value'), ('ProcessValue', 'D_TL'), 'text list TL_Value, output'),
        ('GIO_GL_Value', 'GraphicIOField', 120, 80, '', link('PictureList', 'GL_Value'), ('ProcessValue', 'D_TL'), 'graphic list GL_Value, tag D_TL'),
        ('GIO_GL_Bit', 'GraphicIOField', 120, 80, '', link('PictureList', 'GL_Bit'), ('ProcessValue', 'D_Bool'), 'graphic list GL_Bit, tag D_Bool'),
        ('GIO_GL_BitNo', 'GraphicIOField', 120, 80, '', link('PictureList', 'GL_BitNo'), ('ProcessValue', 'D_Bits'), 'graphic list GL_BitNo, tag D_Bits'),
        ('GIO_GL_Value_Out', 'GraphicIOField', 120, 80, '<Mode>Output</Mode>', link('PictureList', 'GL_Value'), ('ProcessValue', 'D_TL'), 'graphic list GL_Value, output'),
    ]
    pages.append(('C5_Lists', li))
    # buttons and switches
    bt = [
        ('Btn_Text', 'Button', 120, 50, '<Mode>Text</Mode>', '', None, 'mode Text'),
        ('Btn_Graphic', 'Button', 120, 90, '<Mode>Graphic</Mode>', link('PictureOff', 'G_PNG') + link('PictureOn', 'G_JPG'), None, 'mode Graphic, off G_PNG, on G_JPG'),
        ('Btn_GraphicAndText', 'Button', 120, 90, '<Mode>GraphicAndText</Mode>', link('PictureOff', 'G_SVG') + link('PictureOn', 'G_EMF'), None, 'mode GraphicAndText'),
        ('Btn_TextList', 'Button', 160, 50, '<Mode>TextList</Mode>', link('TextList', 'TL_Value'), ('ProcessValue', 'D_TL'), 'mode TextList, TL_Value, D_TL'),
        ('Btn_GraphicList', 'Button', 120, 90, '<Mode>GraphicList</Mode>', link('PictureList', 'GL_Value'), ('ProcessValue', 'D_TL'), 'mode GraphicList, GL_Value, D_TL'),
        ('Btn_Invisible', 'Button', 120, 50, '<Mode>Invisible</Mode>', '', None, 'mode Invisible'),
        ('Sw_Switch', 'Switch', 120, 40, '<Mode>Switch</Mode>', '', ('ProcessValue', 'D_Bool'), 'mode Switch, D_Bool'),
        ('Sw_Text', 'Switch', 120, 40, '<Mode>TextSwitchButton</Mode>', '', ('ProcessValue', 'D_Bool'), 'mode TextSwitchButton, D_Bool'),
        ('Sw_Graphic', 'Switch', 120, 90, '<Mode>GraphicSwitchButton</Mode>', link('PictureOff', 'G_BMP') + link('PictureOn', 'G_GIF'), ('ProcessValue', 'D_Bool'), 'mode GraphicSwitchButton, off G_BMP, on G_GIF, D_Bool'),
    ]
    pages.append(('C5_Buttons', bt))
    return pages


def page_xml(name, number, objs, width, height, nxt, table, template):
    ids = Ids()
    cw = max(o[2] for o in objs) + 16
    ch = max(o[3] for o in objs) + 22
    cols, rows = grid(width, height, cw, ch)
    cols = max(cols, 1)
    out = []
    for idx, (obj, kind, w, h, attrs, links, pv, desc) in enumerate(objs[:cols * rows]):
        x = 6 + (idx % cols) * cw
        y = 40 + (idx // cols) * ch
        children = prop(ids, pv[0], pv[1]) if pv else ''
        if kind in ('Button', 'Switch') and 'Text' in desc or kind == 'Button':
            children += text(ids, 'TextOff', obj + ' off') + text(ids, 'TextOn', obj + ' on')
        out.append(item(ids, kind, obj, x, y, w, h, attrs, links, children))
        out.append(label(ids, obj, x, y + h + 2, cw - 4))
        table.append({'screen': name, 'object': obj, 'type': kind, 'details': desc, 'left': x, 'top': y, 'width': w, 'height': h})
    if len(objs) > cols * rows:
        raise SystemExit(f'{name}: {len(objs)} objects do not fit ({cols}x{rows})')
    out += bottom_bar(ids, width, height, nxt)
    return screen(name, number, [('Layer_0', out, True)], template)


def struct_xml(name, number, width, height, nxt, table, template):
    """Groups, layers (one hidden in the engineering), pop-up and slide-in buttons."""
    ids = Ids()
    l0, l1, l2, l3 = [], [], [], []
    grp = (f'<Hmi.Screen.Group ID="{ids()}" CompositionName="ScreenItems"><AttributeList><ObjectName>Group_A</ObjectName></AttributeList><ObjectList>'
           + item(ids, 'Rectangle', 'GA_Rect', 10, 50, 120, 60, '<BackColor>255, 0, 0</BackColor><UseDesignColorSchema>false</UseDesignColorSchema>')
           + item(ids, 'Circle', 'GA_Circle', 140, 50, 60, 60, '<BackColor>0, 0, 255</BackColor><UseDesignColorSchema>false</UseDesignColorSchema>')
           + f'<Hmi.Screen.Group ID="{ids()}" CompositionName="ScreenItems"><AttributeList><ObjectName>Group_B</ObjectName></AttributeList><ObjectList>'
           + item(ids, 'Ellipse', 'GB_Ellipse', 210, 50, 100, 60, '<BackColor>0, 160, 0</BackColor><UseDesignColorSchema>false</UseDesignColorSchema>')
           + item(ids, 'TextField', 'GB_Text', 210, 115, 100, 20, children=text(ids, 'Text', 'Group_B'))
           + '</ObjectList></Hmi.Screen.Group></ObjectList></Hmi.Screen.Group>')
    l0.append(grp)
    l0.append(label(ids, 'Group_A (contains Group_B)', 10, 140, 300))
    table += [{'screen': name, 'object': o, 'type': t, 'details': d} for o, t, d in (
        ('Group_A', 'Group', 'GA_Rect, GA_Circle, Group_B'), ('Group_B', 'Group', 'GB_Ellipse, GB_Text (nested group)'))]
    # layers: overlapping rectangles, layer 0 at the bottom; layer 2 is hidden in the engineering (VisibleES false)
    for idx, (lst, color) in enumerate(((l0, '255, 128, 0'), (l1, '0, 160, 160'), (l2, '160, 0, 160'), (l3, '255, 255, 0'))):
        x, y = 350 + idx * 30, 50 + idx * 20
        lst.append(item(ids, 'Rectangle', f'Layer{idx}_Rect', x, y, 100, 60, f'<BackColor>{color}</BackColor><UseDesignColorSchema>false</UseDesignColorSchema>'))
        lst.append(item(ids, 'TextField', f'Layer{idx}_Text', x + 2, y + 2, 60, 16, children=text(ids, 'Text', f'Layer {idx}')))
        table.append({'screen': name, 'object': f'Layer{idx}_Rect', 'type': 'Rectangle', 'details': f'layer {idx}, back {color}'
                      + (', layer hidden in the engineering (VisibleES false)' if idx == 2 else '')})
    l0.append(label(ids, 'Layers 0-3 (layer 2 VisibleES false)', 350, 200, 300))
    l0 += bottom_bar(ids, width, height, nxt, extra=(('Popup', 'Popup', POPUP), ('SlideIn', 'Slide', SLIDEIN)))
    return screen(name, number, [('Layer_0', l0, True), ('Layer_1', l1, True), ('Layer_2', l2, False), ('Layer_3', l3, True)], template)


def template_xml(width):
    ids = Ids()
    items = [item(ids, 'Rectangle', 'T_Header', 0, 0, width, 34, '<BackColor>0, 70, 140</BackColor><UseDesignColorSchema>false</UseDesignColorSchema>'),
             item(ids, 'TextField', 'T_Title', 8, 6, 300, 22, '<ForeColor>255, 255, 255</ForeColor><UseDesignColorSchema>false</UseDesignColorSchema>',
                  children=text(ids, 'Text', 'Template T_C05 (header)')),
             item(ids, 'DateTimeField', 'T_Clock', width - 190, 4, 180, 26)]
    lay = ('    <Hmi.Screen.ScreenLayer ID="900" CompositionName="Layers"><AttributeList><Index>0</Index><Name>Layer_0</Name>'
           '</AttributeList><ObjectList>\n' + '\n'.join(items) + '\n    </ObjectList></Hmi.Screen.ScreenLayer>\n')
    return (HEAD + '  <Hmi.Screen.ScreenTemplate ID="0"><AttributeList><Name>T_C05</Name></AttributeList><ObjectList>\n' + lay
            + '  </ObjectList></Hmi.Screen.ScreenTemplate>\n' + TAIL)


def popup_xml():
    ids = Ids()
    items = [item(ids, 'Rectangle', 'Pop_Back', 0, 0, 300, 200, '<BackColor>255, 255, 200</BackColor><UseDesignColorSchema>false</UseDesignColorSchema>'),
             item(ids, 'TextField', 'Pop_Title', 10, 10, 280, 20, children=text(ids, 'Text', 'Pop-up screen Popup_C05')),
             item(ids, 'GraphicView', 'Pop_Graphic', 10, 40, 120, 80, '<FitToLargest>false</FitToLargest>', link('Picture', 'G_PNG')),
             item(ids, 'IOField', 'Pop_Value', 150, 60, 120, 30, '<FormatPattern>s99999</FormatPattern>', children=prop(ids, 'ProcessValue', 'D_Int')),
             button(ids, 'Pop_Close', 'Close', 200, 160, 80, [POPUP])]
    lay = ('    <Hmi.Screen.ScreenLayer ID="900" CompositionName="Layers"><AttributeList><Index>0</Index><Name>Layer_0</Name>'
           '</AttributeList><ObjectList>\n' + '\n'.join(items) + '\n    </ObjectList></Hmi.Screen.ScreenLayer>\n')
    return (HEAD + '  <Hmi.Screen.ScreenPopup ID="0"><AttributeList><Height>200</Height><Name>Popup_C05</Name><Width>300</Width>'
            '</AttributeList><ObjectList>\n' + lay + '  </ObjectList></Hmi.Screen.ScreenPopup>\n' + TAIL)


def slidein_xml(width):
    ids = Ids()
    items = [item(ids, 'TextField', 'SI_Title', 10, 10, 300, 20, children=text(ids, 'Text', 'Slide-in screen Top')),
             item(ids, 'Bar', 'SI_Bar', 330, 10, 200, 40, '<BarOrientation>Right</BarOrientation>', children=prop(ids, 'ProcessValue', 'D_Pct')),
             item(ids, 'IOField', 'SI_Value', 560, 10, 120, 30, '<FormatPattern>s99999</FormatPattern>', children=prop(ids, 'ProcessValue', 'D_Int'))]
    lay = ('    <Hmi.Screen.ScreenLayer ID="900" CompositionName="Layers"><AttributeList><Index>0</Index><Name>Layer_0</Name>'
           '</AttributeList><ObjectList>\n' + '\n'.join(items) + '\n    </ObjectList></Hmi.Screen.ScreenLayer>\n')
    return (HEAD + '  <Hmi.Screen.ScreenSlidein ID="0"><AttributeList><Activate>true</Activate><Dimension>120</Dimension>'
            '<SlideinType>Top</SlideinType><Visibility>FadeOut</Visibility></AttributeList><ObjectList>\n' + lay
            + '  </ObjectList></Hmi.Screen.ScreenSlidein>\n' + TAIL)


def graphiclist_xml(name, rng, entries):
    ids = Ids()
    es = ''
    for et, f, t, g in entries:
        dflt = 'true' if et == 'Default' else 'false'
        et = 'SingleValue' if et == 'Default' else et
        es += (f'<Hmi.TextGraphicList.GraphicListEntry ID="{ids()}" CompositionName="Entries"><AttributeList><DefaultEntry>{dflt}</DefaultEntry>'
               f'<EntryType>{et}</EntryType><From>{f}</From><To>{t}</To></AttributeList><LinkList>{link("Picture", g)}</LinkList>'
               f'</Hmi.TextGraphicList.GraphicListEntry>')
    return (HEAD + f'  <Hmi.TextGraphicList.GraphicList ID="0"><AttributeList><ListRange>{rng}</ListRange><Name>{name}</Name></AttributeList>'
            f'<ObjectList>{es}</ObjectList></Hmi.TextGraphicList.GraphicList>\n' + TAIL)


def main():
    out, width, height = Path(sys.argv[1]), int(sys.argv[2]), int(sys.argv[3])
    for d in ('graphiclists', 'template', 'popup', 'slidein', 'screens'):
        (out / d).mkdir(parents=True, exist_ok=True)
    (out / 'vbs').mkdir(parents=True, exist_ok=True)
    code = 'ShowSlideInScreen "Slide-in screen top", hmiToggle'  # name of the slide-in screen as in the project tree
    if len(sys.argv) > 4 and sys.argv[4] == 'rt':  # WinCC RT Advanced has no slide-in screens; the call breaks all VB scripts
        code = "' slide-in screens are not available on WinCC RT Advanced"
    (out / 'vbs' / 'C5_ToggleSlideIn.xml').write_text(
        HEAD + f'  <Hmi.RuntimeScripting.VBScript ID="0"><AttributeList><Code>{escape(code)}</Code><Name>C5_ToggleSlideIn</Name>'
        '<Type>Sub</Type></AttributeList></Hmi.RuntimeScripting.VBScript>\n' + TAIL, encoding='utf-8')
    for name, (rng, entries) in GRAPHICLISTS.items():
        (out / 'graphiclists' / f'{name}.xml').write_text(graphiclist_xml(name, rng, entries), encoding='utf-8')
    (out / 'template' / 'T_C05.xml').write_text(template_xml(width), encoding='utf-8')
    (out / 'popup' / 'Popup_C05.xml').write_text(popup_xml(), encoding='utf-8')
    (out / 'slidein' / 'Top.xml').write_text(slidein_xml(width), encoding='utf-8')
    pages = []
    for base, objs in build(width, height):  # split a page that does not fit into the screen into several
        cw = max(o[2] for o in objs) + 16
        ch = max(o[3] for o in objs) + 22
        cols, rows = grid(width, height, cw, ch)
        per = cols * rows
        chunks = [objs[i:i + per] for i in range(0, len(objs), per)]
        for k, chunk in enumerate(chunks):
            pages.append((base if len(chunks) == 1 else f'{base}_{chr(97 + k)}', chunk))
    names = [p[0] for p in pages] + ['C5_Structure']
    table = []
    for i, (name, objs) in enumerate(pages):
        (out / 'screens' / f'{name}.xml').write_text(
            page_xml(name, 500 + i, objs, width, height, names[(i + 1) % len(names)], table, 'T_C05'), encoding='utf-8')
    (out / 'screens' / 'C5_Structure.xml').write_text(
        struct_xml('C5_Structure', 500 + len(pages), width, height, names[0], table, 'T_C05'), encoding='utf-8')
    (out / 'instances.json').write_text(json.dumps(table, indent=1), encoding='utf-8')
    (out / 'screens.json').write_text(json.dumps(names), encoding='utf-8')
    print(len(names), 'screens,', len(table), 'instances')


if __name__ == '__main__':
    main()
