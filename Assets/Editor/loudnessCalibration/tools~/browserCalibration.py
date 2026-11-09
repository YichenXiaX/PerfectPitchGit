"""Extract the actual WebGL AAC bank, then serve the local calibration renderer.
Preparation needs UnityPy 1.25.4; serving uses only Python's standard library.
"""
import argparse,hashlib,json,shutil,struct
from pathlib import Path,PurePosixPath
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from urllib.parse import urlparse,parse_qs,unquote

def prepare(dataFile,sourceFile,output):
    import UnityPy
    source=json.loads(sourceFile.read_text());clips={c['clipName']:c for c in source['clips']}
    raw=dataFile.read_bytes()
    if not raw.startswith(b'UnityWebData1.0\x00'):raise ValueError('Supply the uncompressed Unity .data file')
    end=struct.unpack_from('<I',raw,16)[0];offset=20;files={}
    while offset<end:
        start,size,nameSize=struct.unpack_from('<III',raw,offset);offset+=12
        name=raw[offset:offset+nameSize].decode();offset+=nameSize;files[name]=raw[start:start+size]
    env=UnityPy.load(files['data.unity3d']);encoded={}
    (output/'clips').mkdir(parents=True,exist_ok=True)
    for obj in env.objects:
        if obj.type.name!='AudioClip':continue
        data=obj.read_typetree();name=data.get('m_Name')
        if name not in clips:continue
        resource=data['m_Resource'];blob=files[PurePosixPath(resource['m_Source']).name][resource['m_Offset']:resource['m_Offset']+resource['m_Size']]
        if name in encoded and encoded[name]!=blob:raise ValueError('Conflicting recordings named '+name)
        encoded[name]=blob
    if set(encoded)!=set(clips):raise ValueError('Build does not contain the complete calibrated piano bank')
    tasks=[]
    for name,clip in clips.items():
        blob=encoded[name];(output/'clips'/(name+'.bin')).write_bytes(blob)
        for index,pitch in enumerate(clip['sampledPitchRatios']):
            tasks.append({'caseId':f'grid-{name}-{index}','clipName':name,'pitchRatio':pitch,
              'duration':max(source['durations']),'playbackStartFrame':clip['playbackStartFrame'],
              'sampleRate':clip['sampleRate'],'encodedSha256':hashlib.sha256(blob).hexdigest()})
    grid={'sourceProfileId':source['profileId'],'sourceProfileSha256':hashlib.sha256(sourceFile.read_bytes()).hexdigest(),
      'sampleRate':48000,'tasks':tasks}
    (output/'browserGrid.json').write_text(json.dumps(grid,indent=2))
    shutil.copyfile(Path(__file__).with_suffix('.html'),output/'browserCalibration.html')
    print(f'Prepared {len(clips)} recordings and {len(tasks)} pitch renderings in {output}')

def serve(root,port):
    root=root.resolve();output=root/'measurement';output.mkdir(exist_ok=True)
    class handler(BaseHTTPRequestHandler):
        def log_message(self,*args):pass
        def do_GET(self):
            path=unquote(urlparse(self.path).path)
            file=(root/('browserCalibration.html' if path=='/' else path.lstrip('/'))).resolve()
            if not file.is_relative_to(root) or not file.is_file():self.send_error(404);return
            self.send_response(200);self.send_header('Content-Type','text/html' if file.suffix=='.html' else 'application/json' if file.suffix=='.json' else 'application/octet-stream')
            self.send_header('Cache-Control','no-store');self.end_headers()
            with file.open('rb') as stream:shutil.copyfileobj(stream,self.wfile)
        def do_POST(self):
            import re
            url=urlparse(self.path);name=parse_qs(url.query).get('name',[''])[0];size=int(self.headers.get('Content-Length','0'))
            if url.path!='/save' or not re.fullmatch(r'[a-zA-Z0-9_.-]+\.(f32|json)',name) or not 0<size<=12*1024*1024:self.send_error(400);return
            (output/name).write_bytes(self.rfile.read(size));self.send_response(204);self.end_headers()
    print(f'Local silent calibration renderer: http://localhost:{port}',flush=True)
    ThreadingHTTPServer(('127.0.0.1',port),handler).serve_forever()

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--data',type=Path);parser.add_argument('--source-profile',dest='sourceProfile',type=Path)
    parser.add_argument('--directory',type=Path,required=True);parser.add_argument('--serve',action='store_true');parser.add_argument('--port',type=int,default=5191)
    args=parser.parse_args()
    if args.serve:serve(args.directory,args.port)
    elif args.data and args.sourceProfile:prepare(args.data,args.sourceProfile,args.directory)
    else:parser.error('Choose --serve or supply --data and --source-profile')
