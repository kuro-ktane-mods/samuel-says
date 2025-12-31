using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainScreen : MonoBehaviour {

    private const float MorseTimeUnit = 0.2f;
    private const float ColourBrightness = 0.9f;

    private bool _skipPause = false;

    [SerializeField] private TextMesh _colourblindText;
    [SerializeField] private MeshRenderer _display;
    [SerializeField] private AudioSource[] _beeps;

    private MeshRenderer _colourblindRenderer;

    private readonly string[] _colourNames = new string[] {
        "Red",
        "Yellow",
        "Green",
        "Blue"
    };
    private readonly Color[] _colourList = new Color[] {
        Color.red,
        Color.yellow,
        Color.green,
        Color.blue
    };
    private Coroutine _displaySequence;
    private bool _isSequencePaused = false;

    private void Awake() {
        _display.enabled = false;
        _colourblindRenderer = _colourblindText.GetComponent<MeshRenderer>();
        _colourblindRenderer.enabled = false;
    }

    public void ToggleMute() {
        // Toggle between 0 and 0.5.
        foreach (AudioSource beep in _beeps) {
            beep.volume = 0.5f - beep.volume;
        }
    }

    public void ToggleColourblindMode() {
        _colourblindRenderer.enabled = !_colourblindRenderer.enabled;
    }

    private void DisplayColour(ButtonColour colour) {
        _display.enabled = true;
        _display.material.color = _colourList[(int)colour] * ColourBrightness;
        _colourblindText.text = _colourNames[(int)colour];
        _beeps[(int)colour].Play();
    }

    private void StopDisplayingColour() {
        _display.enabled = false;
        _colourblindText.text = string.Empty;
        foreach (AudioSource beep in _beeps) {
            beep.Stop();
        }
    }

    public void PlaySequence(ColouredSymbol[] sequence, bool skipPause = false) {
        PlaySequences(new List<ColouredSymbol[]>() { sequence }, skipPause);
    }

    public void PlaySequences(List<ColouredSymbol[]> sequences, bool skipPause = false) {
        _isSequencePaused = false;
        if (_displaySequence != null) {
            StopCoroutine(_displaySequence);
        }
        _skipPause = skipPause;
        _displaySequence = StartCoroutine(DisplaySequences(sequences));
    }

    public void PauseSequence() {
        _isSequencePaused = true;
        StopDisplayingColour();
    }

    public void UnpauseSequence() {
        _isSequencePaused = false;
    }

    public void StopSequence() {
        if (_displaySequence != null) {
            StopCoroutine(_displaySequence);
        }
        StopDisplayingColour();
    }

    private IEnumerator DisplaySequences(List<ColouredSymbol[]> sequences) {
        ColouredSymbol[] currentSequence = sequences[0];
        float elapsedTime;
        float waitTime;

        foreach (ColouredSymbol symbol in currentSequence) {
            while (_isSequencePaused) {
                yield return null;
            }
            int flashLength = (symbol.Symbol == '-') ? 3 : 1;
            DisplayColour(symbol.Colour);

            // Wait for waitTime seconds.
            waitTime = MorseTimeUnit * flashLength;
            for (elapsedTime = 0; elapsedTime < waitTime; elapsedTime += Time.deltaTime) {
                yield return null;
            }

            StopDisplayingColour();

            waitTime = MorseTimeUnit;
            for (elapsedTime = 0; elapsedTime < waitTime; elapsedTime += Time.deltaTime) {
                yield return null;
            }
        }

        if (!_skipPause) {
            waitTime = 2 * MorseTimeUnit;
            for (elapsedTime = 0; elapsedTime < waitTime; elapsedTime += Time.deltaTime) {
                yield return null;
            }
        }

        yield return null;
        sequences.Add(sequences[0]);
        sequences.RemoveAt(0);
        _displaySequence = StartCoroutine(DisplaySequences(sequences));
    }
}
