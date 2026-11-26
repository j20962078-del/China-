import pickle,json,re,collections,glob,msgpack,os,shutil
objs=pickle.load(open('/tmp/pobj.pkl','rb'))
nodes=objs[31]; meshes={m[0]:m for m in objs[32]}
base=glob.glob('/home/claude/prisma/*')[0]
u=msgpack.Unpacker(raw=False,strict_map_key=False); u.feed(open(base+'/project.proj','rb').read()); proj=list(u)
mats={}
for m in proj[12]:
    pr={k:v for k,v,t in m[2]}
    col=json.loads(pr['color']); tex=pr['texture']
    mats[m[0]]=dict(name=re.sub(r'[^A-Za-z0-9_]+','_',m[1]).strip('_') or 'mat',col=col,tex=tex,alpha=float(pr['alpha']),gloss=float(pr['glossiness']))
# unique names
seen=collections.Counter()
for k,m in mats.items():
    seen[m['name']]+=1
    if seen[m['name']]>1: m['name']+= '_%d'%seen[m['name']]
byid={n[0]:n for n in nodes}
def comp(n,name):
    for c in n[2]:
        if c[0]==name: return {k:v for k,v,t in c[1]}
def path(n):
    r=[]
    while n: r.append(n[1]); n=byid.get(n[3])
    return r[::-1]
os.makedirs('out/kam',exist_ok=True)
vo=[];vt=[];vn=[];lines=[];used=set()
vbase=tbase=nbase=0
stats=collections.defaultdict(lambda:[0,[1e9]*3,[-1e9]*3])
for n in nodes:
    r=comp(n,'PMeshRenderer')
    if not r: 
        t=comp(n,'PTransform'); 
        if n[1]=='PlayerSpawn': print('SPAWN',t)
        continue
    mid=json.loads(r['pMesh'])['id']; mids=json.loads(r['materials'])['ids']
    m=meshes[mid]
    fm,fc,fn,idx,uv,V=m[1],m[2],m[3],m[4],m[5],m[8]
    pth=path(n); top=pth[3] if len(pth)>3 else pth[-1]
    cat=pth[2] if len(pth)>2 else n[1]
    # Prisma coords: assume Y up. write as is.
    for p in V:
        vo.append('v %.5f %.5f %.5f'%tuple(p))
    for p in uv: vt.append('vt %.5f %.5f'%tuple(p))
    for p in fn: vn.append('vn %.5f %.5f %.5f'%tuple(p))
    lines.append('g %s/%s'%(cat,n[1]))
    c=0; curm=None
    for f in range(len(fm)):
        k=fc[f]; mi=mids[min(fm[f],len(mids)-1)]
        if mi!=curm: lines.append('usemtl '+mats[mi]['name']); curm=mi; used.add(mi)
        face=[]
        for j in range(k):
            face.append('%d/%d/%d'%(vbase+idx[c+j]+1,tbase+c+j+1,nbase+f+1))
        c+=k
        lines.append('f '+' '.join(face))
    st=stats[cat]; st[0]+=1
    for p in V:
        for a in range(3):
            st[1][a]=min(st[1][a],p[a]); st[2][a]=max(st[2][a],p[a])
    vbase+=len(V); tbase+=len(uv); nbase+=len(fn)
open('out/kam/kamyshevskoe.obj','w').write('mtllib kamyshevskoe.mtl\n'+'\n'.join(vo)+'\n'+'\n'.join(vt)+'\n'+'\n'.join(vn)+'\n'+'\n'.join(lines)+'\n')
mt=[]
for mi in used:
    m=mats[mi]; c=m['col']
    mt.append('newmtl %s\nKd %.4f %.4f %.4f\nd %.3f'%(m['name'],c['r'],c['g'],c['b'],m['alpha']))
    if m['tex']:
        fn=os.path.basename(m['tex']); shutil.copy(base+'/res/'+fn,'out/kam/'+fn); mt.append('map_Kd '+fn)
    mt.append('')
open('out/kam/kamyshevskoe.mtl','w').write('\n'.join(mt))
print('verts',len(vo),'faces',sum(1 for l in lines if l[0]=='f'))
for k,(n,a,b) in stats.items(): print(k,n,[round(x,1) for x in a],[round(x,1) for x in b])
print('--mats'); 
for mi in used: m=mats[mi]; print(m['name'],[round(m['col'][x],2) for x in 'rgb'],m['tex'],m['alpha'])
