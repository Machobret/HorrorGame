from pathlib import Path
import av, numpy as np, sys, base64
folder=Path(__file__).resolve().parent/'Audio'
name=sys.argv[1];start=float(sys.argv[2]);duration=float(sys.argv[3])
signal=np.load(folder/(name+'_Source.npy'))[int(start*44100):int((start+duration)*44100)]
signal=signal*.8/max(.8,float(np.max(np.abs(signal))))
path=folder/(name+'_Preview.mp3')
output=av.open(str(path),'w');stream=output.add_stream('libmp3lame',rate=44100);stream.layout='mono';stream.bit_rate=48000
frame=av.AudioFrame.from_ndarray(signal.reshape(1,-1),format='fltp',layout='mono');frame.sample_rate=44100
for packet in stream.encode(frame):output.mux(packet)
for packet in stream.encode(None):output.mux(packet)
output.close()
print(base64.b64encode(path.read_bytes()).decode())
