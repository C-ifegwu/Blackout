import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, lfilter
SR=22050
rng=np.random.default_rng(7)
def t(sec): return np.arange(int(SR*sec))/SR
def save(name,x,peak=0.8):
    x=x/ (np.max(np.abs(x))+1e-9)*peak
    wavfile.write(f"/tmp/audiogen/out/{name}.wav",SR,(x*32767).astype(np.int16))
def lp(x,f,o=2):
    b,a=butter(o,f/(SR/2),'low'); return lfilter(b,a,x)
def bp(x,lo,hi,o=2):
    b,a=butter(o,[lo/(SR/2),hi/(SR/2)],'band'); return lfilter(b,a,x)
def loopfade(x,n=2000):
    x=x.copy(); w=np.linspace(0,1,n)
    x[:n]=x[:n]*w + x[-n:]*(1-w); return x[:-n]
def env_ad(n,a,d):
    e=np.ones(n); na=int(a*SR); e[:na]=np.linspace(0,1,na); nd=int(d*SR); e[-nd:]=np.linspace(1,0,nd)**2; return e

# sonar ping: downward sweep with soft tail
tt=t(0.7); f=1500*np.exp(-tt*2.2)+260
ph=2*np.pi*np.cumsum(f)/SR
ping=np.sin(ph)*np.exp(-tt*5.5)+0.3*np.sin(2*ph)*np.exp(-tt*9)
ping[:200]*=np.linspace(0,1,200)
save("sonar_ping",ping,0.7)

# relay tones: two soft pulses with echo, loop 2.4 s
def relay(f0,name):
    L=2.4; tt=t(L); out=np.zeros_like(tt)
    for st in (0.0,0.35):
        m=(tt>=st)&(tt<st+0.5); tl=tt[m]-st
        pulse=(np.sin(2*np.pi*f0*tl)+0.4*np.sin(2*np.pi*2*f0*tl))*np.exp(-tl*5)
        pulse[:150]*=np.linspace(0,1,150)
        out[m]+=pulse
    echo=np.zeros_like(out); d=int(0.28*SR); echo[d:]=out[:-d]*0.35
    out=out+echo
    out[-int(0.2*SR):]*=np.linspace(1,0,int(0.2*SR))
    save(name,out,0.6)
relay(330,"relay_tone_1"); relay(392,"relay_tone_2"); relay(494,"relay_tone_3")

# relay activated: rising arpeggio
tt=t(1.2); a=np.zeros_like(tt)
for i,fr in enumerate((392,494,659,784)):
    s=int(i*0.12*SR); seg=tt[:len(tt)-s]
    a[s:]+=np.sin(2*np.pi*fr*seg)*np.exp(-seg*3.5)*0.5
a[:100]*=np.linspace(0,1,100); save("relay_on",a,0.7)

# tower signal: low radio hum + three rising beeps, 3.2 s loop
L=3.2; tt=t(L)
hum=lp(rng.standard_normal(len(tt)),400)*0.4+0.3*np.sin(2*np.pi*110*tt)
beeps=np.zeros_like(tt)
for i,fr in enumerate((523,659,784)):
    s=0.2+i*0.35; m=(tt>=s)&(tt<s+0.3); tl=tt[m]-s
    beeps[m]+=np.sin(2*np.pi*fr*tl)*np.sin(np.pi*tl/0.3)**2
sig=hum*0.6+beeps*0.8; save("tower_signal",loopfade(sig),0.6)

# generator: 55 Hz sawtooth-ish + rattle noise, 4 s seamless
L=4.0; tt=t(L); base=sum((1.0/k)*np.sin(2*np.pi*55*k*tt) for k in range(1,12))
rattle=bp(rng.standard_normal(len(tt)),120,900)*(0.5+0.5*np.sin(2*np.pi*13*tt))
gen=base*0.6+rattle*0.25; save("generator_loop",gen,0.5)

# market murmur: band-limited noise with syllable-like modulation, 8 s
L=8.0; tt=t(L); n=bp(rng.standard_normal(len(tt)),180,2800)
mod=np.zeros_like(tt)
for k in range(5):
    r=rng.uniform(2.5,5.5); ph0=rng.uniform(0,6.28); mod+=0.5+0.5*np.sin(2*np.pi*r*tt+ph0+0.8*np.sin(2*np.pi*0.3*tt))
mod/=5; murmur=n*(0.25+0.75*mod)
form=bp(rng.standard_normal(len(tt)),500,1200)*mod*0.6
save("market_murmur",loopfade(murmur+form),0.45)

# distant traffic with two danfo horns, 12 s
L=12.0; tt=t(L); rum=lp(rng.standard_normal(len(tt)),220,3)*(0.6+0.4*np.sin(2*np.pi*0.17*tt))
def horn(start,dur,fr):
    m=(tt>=start)&(tt<start+dur); tl=tt[m]-start
    w=(np.sign(np.sin(2*np.pi*fr*tl))*0.5+np.sin(2*np.pi*fr*1.5*tl)*0.3)*np.sin(np.pi*tl/dur)**2
    o=np.zeros_like(tt); o[m]=w; return lp(o,1800)
tr=rum*0.9+horn(2.5,0.35,430)*0.5+horn(3.0,0.5,430)*0.5+horn(8.4,0.6,380)*0.4
save("distant_traffic",loopfade(tr),0.45)
print("done")
