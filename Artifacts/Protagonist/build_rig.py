import bpy
import bmesh
import json
import sys
from pathlib import Path
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from repair_skin_weights import repair_lower_body_weights

ROOT = Path('D:/Pandd/ShinShinzui')
ART = ROOT / 'Assets/Shinzui/Art/Protagonist'
OUT = ROOT / 'Artifacts/Protagonist'
ART.mkdir(parents=True, exist_ok=True)


def bone(name, head, tail, parent=None):
    """<summary>指定した関節位置に変形用ボーンを作成</summary>
    <param name="name">ボーン名</param><param name="head">始点</param>
    <param name="tail">終点</param><param name="parent">親ボーン名</param>
    <returns>作成したボーン</returns>"""
    value = rig.data.edit_bones.new(name)
    value.head = Vector(head) * 1.8
    value.tail = Vector(tail) * 1.8
    if parent:
        value.parent = rig.data.edit_bones[parent]
    if 'Arm' in name or 'Hand' in name or any(f in name for f in fingers):
        value.align_roll(Vector((0, 0, 1)))
    return value


# 元のGLBを変更せず、メートル単位の作業用メッシュへ変換
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath='C:/Users/koton/Downloads/rugged man 3d model.glb')
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
body.name = 'BodyMesh'
body.parent = None
body.scale = (1.8, 1.8, 1.8)
bpy.context.view_layer.objects.active = body
body.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# UV境界の重複頂点を溶接して自動ウェイトを安定させる
bm = bmesh.new()
bm.from_mesh(body.data)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00002)
bm.to_mesh(body.data)
bm.free()
body.data.update()

# Humanoid互換の全身骨格と左右の指を配置
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.object.armature_add()
rig = bpy.context.object
rig.name = 'ProtagonistRig'
rig.data.name = 'ProtagonistSkeleton'
rig.show_in_front = True
bpy.ops.object.mode_set(mode='EDIT')
rig.data.edit_bones.remove(rig.data.edit_bones[0])
fingers = ['Thumb', 'Index', 'Middle', 'Ring', 'Little']
bone('Hips', (0, 0, .49), (0, 0, .56))
bone('Spine', (0, 0, .56), (0, 0, .66), 'Hips')
bone('Chest', (0, 0, .66), (0, 0, .75), 'Spine')
bone('UpperChest', (0, 0, .75), (0, 0, .81), 'Chest')
bone('Neck', (0, 0, .81), (0, -.008, .87), 'UpperChest')
bone('Head', (0, -.008, .87), (0, -.008, .985), 'Neck')
for side, sign in [('Left', 1), ('Right', -1)]:
    bone(side+'Shoulder', (sign*.035, 0, .797), (sign*.135, 0, .79), 'UpperChest')
    bone(side+'UpperArm', (sign*.135, 0, .79), (sign*.278, 0, .785), side+'Shoulder')
    bone(side+'LowerArm', (sign*.278, 0, .785), (sign*.405, -.006, .782), side+'UpperArm')
    bone(side+'Hand', (sign*.405, -.006, .782), (sign*.443, -.006, .782), side+'LowerArm')
    bone(side+'UpperLeg', (sign*.071, 0, .505), (sign*.072, -.008, .285), 'Hips')
    bone(side+'LowerLeg', (sign*.072, -.008, .285), (sign*.074, .005, .075), side+'UpperLeg')
    bone(side+'Foot', (sign*.074, .005, .075), (sign*.075, -.063, .032), side+'LowerLeg')
    bone(side+'Toes', (sign*.075, -.063, .032), (sign*.075, -.09, .025), side+'Foot')

    # 手の甲から確認した各指の中心線を使い、掌側へ曲がる骨軸を統一
    layout = {
        'Thumb': [(.419,-.025,.778),(.434,-.039,.773),(.446,-.046,.771),(.457,-.046,.771)],
        'Index': [(.439,-.026,.783),(.462,-.028,.783),(.480,-.027,.782),(.494,-.027,.781)],
        'Middle': [(.443,-.010,.784),(.468,-.011,.784),(.487,-.011,.782),(.499,-.011,.781)],
        'Ring': [(.440,.003,.784),(.463,.002,.784),(.481,.001,.782),(.495,.001,.781)],
        'Little': [(.435,.015,.783),(.454,.015,.783),(.468,.014,.781),(.480,.013,.780)]
    }
    for finger, points in layout.items():
        parent = side+'Hand'
        for i, suffix in enumerate(['Proximal','Intermediate','Distal']):
            a, b = points[i], points[i+1]
            name = side+finger+suffix
            bone(name, (sign*a[0], a[1], a[2]), (sign*b[0], b[1], b[2]), parent)
            parent = name
bpy.ops.object.mode_set(mode='OBJECT')

# 閉じた作業用メッシュで熱拡散を計算し、元のUV付きメッシュへ転写
proxy = body.copy()
proxy.data = body.data.copy()
bpy.context.collection.objects.link(proxy)
proxy.name = 'WeightProxy'
bpy.ops.object.select_all(action='DESELECT')
proxy.select_set(True)
bpy.context.view_layer.objects.active = proxy
proxy.data.remesh_voxel_size = .006
bpy.ops.object.voxel_remesh()
print('PROXY', len(proxy.data.vertices))
bpy.ops.object.select_all(action='DESELECT')
proxy.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
missing = [v for v in proxy.data.vertices if sum(g.weight for g in v.groups)<.01]
print('PROXY_MISSING',len(missing), flush=True)
if len(missing)>len(proxy.data.vertices)*.05:
    raise RuntimeError('Proxy weight solve failed')
if missing:
    from mathutils.kdtree import KDTree
    valid = [v for v in proxy.data.vertices if sum(g.weight for g in v.groups)>.01]
    tree = KDTree(len(valid))
    for vertex in valid:
        tree.insert(vertex.co,vertex.index)
    tree.balance()
    for vertex in missing:
        _,index,_ = tree.find(vertex.co)
        for group in proxy.data.vertices[index].groups:
            proxy.vertex_groups[group.group].add([vertex.index],group.weight,'REPLACE')

bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bpy.context.view_layer.objects.active = body
for group in proxy.vertex_groups:
    body.vertex_groups.new(name=group.name)
transfer = body.modifiers.new('TransferSkin', 'DATA_TRANSFER')
transfer.object = proxy
transfer.use_vert_data = True
transfer.data_types_verts = {'VGROUP_WEIGHTS'}
transfer.vert_mapping = 'POLYINTERP_NEAREST'
bpy.ops.object.modifier_apply(modifier=transfer.name)
body.parent = rig
deform = body.modifiers.new('Armature', 'ARMATURE')
deform.object = rig
bpy.data.objects.remove(proxy, do_unlink=True)
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
repair_lower_body_weights(body)
unweighted = [v.index for v in body.data.vertices if sum(g.weight for g in v.groups) < .99]
if unweighted:
    raise RuntimeError('Unweighted vertices: '+str(len(unweighted)))

# 首の位置で切断し、共有骨格のまま頭と胴体を別メッシュ化
cut = .853 * 1.8
bm = bmesh.new()
bm.from_mesh(body.data)
bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces), dist=.000001,
                      plane_co=(0,0,cut), plane_no=(0,0,1), clear_inner=False, clear_outer=False)
bm.to_mesh(body.data)
bm.free()
for vertex in body.data.vertices:
    vertex.select = False
for edge in body.data.edges:
    edge.select = False
for poly in body.data.polygons:
    poly.select = all(body.data.vertices[v].co.z >= cut-.00001 for v in poly.vertices)
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.context.tool_settings.mesh_select_mode = (False, False, True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.separate(type='SELECTED')
bpy.ops.object.mode_set(mode='OBJECT')
head = next(o for o in bpy.context.selected_objects if o != body)
head.name = 'HeadMesh'

# 首の切断面だけを塞ぎ、頭を隠した際の内側の抜けを防止
for obj in [body, head]:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    edges = [e for e in bm.edges if e.is_boundary and all(abs(v.co.z-cut)<.0001 for v in e.verts)]
    if edges:
        bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    if len(obj.data.polygons) == 0:
        raise RuntimeError('Empty split mesh: '+obj.name)

# 埋め込みテクスチャを共通素材として取り出す
material = body.data.materials[0]
material.name = 'Protagonist'
for node in material.node_tree.nodes:
    if node.type == 'TEX_IMAGE' and node.image:
        node.image.filepath_raw = str(ART / 'Protagonist_BaseColor.png')
        node.image.file_format = 'PNG'
        node.image.save()

# 編集可能なBlendとUnity用FBXをTポーズのバインド状態で保存
bpy.ops.object.select_all(action='DESELECT')
for obj in [body, head, rig]:
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'ProtagonistRig.blend'))
bpy.ops.export_scene.fbx(filepath=str(ART / 'Protagonist.fbx'), use_selection=True,
    object_types={'ARMATURE','MESH'}, add_leaf_bones=False, bake_anim=False,
    axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', use_mesh_modifiers=True,
    apply_scale_options='FBX_SCALE_ALL')
bpy.ops.export_scene.gltf(filepath=str(OUT / 'ProtagonistRig.glb'), use_selection=True,
    export_format='GLB', export_animations=False)
report = {'bones': len(rig.data.bones), 'unweighted': len(unweighted),
          'meshes': {o.name: {'vertices':len(o.data.vertices),'faces':len(o.data.polygons)} for o in [body,head]}}
(OUT / 'rig-report.json').write_text(json.dumps(report, indent=2))
print('RIG_REPORT',json.dumps(report))
