import json, os, struct, subprocess
exe = os.path.abspath('standalone/dotSharK.exe')
manifest = os.path.abspath('standalone/com.rampagex.dotshark.json')
p = subprocess.Popen([exe, manifest, 'dotshark@rampagex.github.io'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
msg = b'{"action":"ping"}'
out, err = p.communicate(struct.pack('<I', len(msg)) + msg, timeout=15)
assert p.returncode == 0, (p.returncode, err)
assert len(out) >= 4 and len(out) == 4 + struct.unpack('<I', out[:4])[0], out
assert json.loads(out[4:]).get('ok') is True, out
print('PASS Firefox native messaging raw protocol ping')
