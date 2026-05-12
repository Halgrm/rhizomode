#ifndef RHIZOMODE_AUDIO_WAVEFORM
#define RHIZOMODE_AUDIO_WAVEFORM

StructuredBuffer<float> _RhizomodeAudioWaveform;
int _RhizomodeAudioWaveformSize = 512;

float RhizomodeAudioWaveform(in uint index)
{
    return _RhizomodeAudioWaveform[index % (uint)_RhizomodeAudioWaveformSize];
}

uint RhizomodeAudioWaveformSize()
{
    return (uint)_RhizomodeAudioWaveformSize;
}

#endif
