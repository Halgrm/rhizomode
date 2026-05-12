#ifndef RHIZOMODE_AUDIO_SPECTRUM
#define RHIZOMODE_AUDIO_SPECTRUM

StructuredBuffer<float> _RhizomodeAudioSpectrum;
int _RhizomodeAudioSpectrumSize = 512;

float RhizomodeAudioSpectrum(in uint index)
{
    return _RhizomodeAudioSpectrum[index % (uint)_RhizomodeAudioSpectrumSize];
}

#endif
