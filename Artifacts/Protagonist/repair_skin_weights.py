from mathutils.kdtree import KDTree


def repair_lower_body_weights(body):
    """<summary>腰より下へ誤転写された腕のウェイトを近傍の正常なウェイトで補正</summary>
    <param name="body">Tポーズのメッシュ</param>
    <returns>補正した頂点数</returns>"""
    arm_names = ('Arm', 'Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little')
    bad = {v.index for v in body.data.vertices if v.co.z < 1.2 and any(
        g.weight > .01 and any(name in body.vertex_groups[g.group].name for name in arm_names)
        for g in v.groups)}

    # 誤った島を参照元から除き、同じ位置の衣服へ追従させる
    valid = [v for v in body.data.vertices if v.index not in bad]
    tree = KDTree(len(valid))
    for vertex in valid:
        tree.insert(vertex.co, vertex.index)
    tree.balance()
    for index in bad:
        _, nearest, _ = tree.find(body.data.vertices[index].co)
        weights = [(g.group, g.weight) for g in body.data.vertices[nearest].groups]
        for group in body.vertex_groups:
            group.remove([index])
        for group, weight in weights:
            body.vertex_groups[group].add([index], weight, 'REPLACE')
    return len(bad)
