#!/usr/bin/env python3
"""Convert the WPF MainWindow.xaml (MaterialDesign) to Avalonia axaml."""
import re
from lxml import etree

SRC = 'MirrorsEdgeTweaks-original/MirrorsEdgeTweaks/MainWindow.xaml'
DST = 'MirrorsEdgeTweaksLinux/MainWindow.axaml'

WPF = 'http://schemas.microsoft.com/winfx/2006/xaml/presentation'
XAML_X = 'http://schemas.microsoft.com/winfx/2006/xaml'
MD = 'http://materialdesigninxaml.net/winfx/xaml/themes'
D = 'http://schemas.microsoft.com/expression/blend/2008'
MC = 'http://schemas.openxmlformats.org/markup-compatibility/2006'
BEH = 'clr-namespace:MirrorsEdgeTweaks.Behaviors'
AVALONIA = 'https://github.com/avaloniaui'
ICONS = 'https://github.com/projektanker/icons.avalonia'

parser = etree.XMLParser(remove_comments=False)
tree = etree.parse(SRC, parser)
root = tree.getroot()

def q(ns, name): return '{%s}%s' % (ns, name)

def kebab(name):
    return re.sub(r'(?<!^)(?=[A-Z0-9])', '-', name).lower().replace('--', '-')

STYLE_CLASS_MAP = {
    'MaterialDesignRaisedButton': 'accent',
    'MaterialDesignOutlinedButton': 'outlined',
    'MaterialDesignIconButton': 'icon-btn',
}

# Walk and transform
for el in list(root.iter()):
    tag_ns = etree.QName(el).namespace if isinstance(el.tag, str) else None
    tag_name = etree.QName(el).localname if isinstance(el.tag, str) else None
    if not isinstance(el.tag, str):
        continue

    # Remove style/resource/trigger subtrees
    if tag_name in ('Style', 'Trigger', 'Setter') or tag_name.endswith('.Style') or tag_name.endswith('.Resources'):
        el.getparent().remove(el)
        continue

    # PackIcon -> i:Icon
    if tag_ns == MD and tag_name == 'PackIcon':
        kind = el.get('Kind', 'Information')
        el.tag = q(ICONS, 'Icon')
        el.attrib.pop('Kind', None)
        el.attrib.pop('Width', None)
        el.attrib.pop('Height', None)
        el.set('Value', 'mdi-' + kebab(kind))
        continue

    # DialogHost wrapper -> unwrap (hoist children into parent)
    if tag_ns == MD and tag_name == 'DialogHost':
        parent = el.getparent()
        idx = list(parent).index(el)
        for child in reversed(list(el)):
            parent.insert(idx, child)
        parent.remove(el)
        continue

    # StatusBar/StatusBarItem -> DockPanel/ContentControl
    if tag_name == 'StatusBar':
        el.tag = q(WPF, 'DockPanel')
        el.set('LastChildFill', 'False')
    elif tag_name == 'StatusBarItem':
        el.tag = q(WPF, 'ContentControl')
        if el.get('HorizontalAlignment') == 'Right':
            el.set('DockPanel.Dock', 'Right')
        else:
            el.set('DockPanel.Dock', 'Left')
    elif tag_name == 'Separator':
        el.tag = q(WPF, 'Border')
        el.set('Classes', 'separator')

    # Attribute transforms
    attrs = dict(el.attrib)
    for name, value in attrs.items():
        # strip designer/behavior/md attached attrs
        if name.startswith('{') and (D in name or MC in name or BEH in name or MD in name):
            del el.attrib[name]
            continue
        if name == 'Visibility':
            del el.attrib[name]
            el.set('IsVisible', 'True' if value == 'Visible' else 'False')
        elif name == 'ToolTip':
            del el.attrib[name]
            el.set('ToolTip.Tip', value)
        elif name == 'Style':
            m = re.match(r'\{(?:Static|Dynamic)Resource (\w+)\}', value)
            del el.attrib[name]
            if m and m.group(1) in STYLE_CLASS_MAP:
                existing = el.get('Classes', '')
                cls = STYLE_CLASS_MAP[m.group(1)]
                el.set('Classes', (existing + ' ' + cls).strip())
        elif name in ('TextElement.Foreground', 'KeyboardNavigation.TabNavigation',
                      'SnapsToDevicePixels', 'UseLayoutRounding', 'ResizeMode', 'Icon'):
            del el.attrib[name]
        elif name == 'Loaded' and etree.QName(el).localname == 'Window':
            del el.attrib[name]  # rewired in code-behind ctor

xml = etree.tostring(root, pretty_print=False, encoding='unicode')

# Namespace surgery on the serialized text
xml = xml.replace('xmlns="%s"' % WPF, 'xmlns="%s"' % AVALONIA)
xml = xml.replace('xmlns:materialDesign="%s"' % MD, 'xmlns:i="%s"' % ICONS)
xml = xml.replace('xmlns:d="%s"' % D, '')
xml = xml.replace('xmlns:mc="%s"' % MC, '')
xml = xml.replace('xmlns:behaviors="%s"' % BEH, '')
xml = xml.replace('mc:Ignorable="d"', '')
# lxml will have emitted icon ns as something like ns0; normalize
xml = re.sub(r'xmlns:ns\d+="%s"' % re.escape(ICONS), '', xml)
xml = re.sub(r'ns\d+:Icon', 'i:Icon', xml)
xml = xml.replace('<Icon ', '<i:Icon ')  # safety, shouldn't trigger
# WPF default-ns leftovers fine otherwise

open(DST, 'w').write(xml)
print("written", DST, len(xml), "chars")
