exec(open('conv.py').read().split("os.makedirs")[0])
import struct,json,collections,os,shutil
import numpy as np
def cat_of(n):
    s=n[1]
    for p in ('Dock','Forest','Lamps','LilyPads','Reeds'):
        if s.startswith(p): return p
    if s in('Fences','GrassTufts','Ground','Road','ShoreRocks','Water'): return s
    return 'Village'
groups=collections.OrderedDict()
ground=None
for n in nodes:
    r=comp(n,'PMeshRenderer')
    if not r: continue
    m=meshes[json.loads(r['pMesh'])['id']]; mids=json.loads(r['materials'])['ids']
    cat=cat_of(n)
    fm,fc,fn,idx,uv,V=m[1],m[2],m[3],m[4],m[5],m[8]
    if cat=='Ground': ground=np.array(V)
    c=0
    for f in range(len(fm)):
        k=fc[f]; mi=mids[min(fm[f],len(mids)-1)]
        g=groups.setdefault((cat,mi),dict(map={},pos=[],nrm=[],uv=[],idx=[]))
        nn=fn[f]; nb=tuple(int(round(max(-1,min(1,x))*127)) for x in nn)
        ids=[]
        for j in range(k):
            p=V[idx[c+j]]; t=uv[c+j]
            key=(round(p[0],4),round(p[1],4),round(p[2],4),round(t[0],4),round(t[1],4),nb)
            i=g['map'].get(key)
            if i is None:
                i=len(g['pos']); g['map'][key]=i; g['pos'].append(p); g['nrm'].append(nb); g['uv'].append(t)
            ids.append(i)
        c+=k
        for j in range(1,k-1): g['idx']+= [ids[0],ids[j],ids[j+1]]
def s(b,x): 
    e=x.encode('utf-8'); b+=struct.pack('<H',len(e))+e
mode={'Water':2,'GrassTuft':1,'Cattail':1,'Reed':1,'Lily':1,'LilyFlower':1}
b=bytearray(b'KAM1')
used=sorted({mi for (_,mi) in groups})
b+=struct.pack('<i',len(used))
for mi in used:
    m=mats[mi]; s(b,m['name']); c=m['col']
    b+=struct.pack('<4f',c['r'],c['g'],c['b'],m['alpha'])
    s(b,os.path.basename(m['tex']).replace('.png','') if m['tex'] else '')
    b+=struct.pack('<if',mode.get(m['name'],0),m['gloss'])
b+=struct.pack('<i',len(groups))
tv=0
for (cat,mi),g in groups.items():
    s(b,cat); s(b,mats[mi]['name'])
    nv=len(g['pos']); ni=len(g['idx']); tv+=nv
    b+=struct.pack('<ii',nv,ni)
    b+=np.array(g['pos'],dtype='<f4').tobytes()
    b+=np.array(g['nrm'],dtype='<i1').tobytes()
    b+=np.array(g['uv'],dtype='<f4').tobytes()
    b+=np.array(g['idx'],dtype='<i4').tobytes()
    print(cat,mats[mi]['name'],nv,ni//3)
os.makedirs('out/res',exist_ok=True)
open('out/res/kam_scene.bytes','wb').write(b)
# heightmap
xs=np.unique(np.round(ground[:,0],3)); zs=np.unique(np.round(ground[:,2],3))
H=np.zeros((171,171),dtype='<f4')
xi={x:i for i,x in enumerate(xs)}; zi={z:i for i,z in enumerate(zs)}
for p in ground: H[zi[round(p[2],3)],xi[round(p[0],3)]]=p[1]
hb=struct.pack('<iiffff',171,171,float(ground[:,0].min()),float(ground[:,0].max()),float(ground[:,2].min()),float(ground[:,2].max()))+H.tobytes()
open('out/res/kam_height.bytes','wb').write(hb)
print('total verts',tv,'bytes',len(b),'H',H.min(),H.max())
for f in set(os.path.basename(m['tex']) for m in mats.values() if m['tex']):
    shutil.copy(base+'/res/'+f,'out/res/'+f.replace('.png','.bytes'))
print(sorted(os.listdir('out/res')))
