using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Rnd = UnityEngine.Random;

public class SamuelSaysModule : MonoBehaviour {

    // ! Look at the README for information on where to look first for bugs.

    [SerializeField] private ColouredButton[] _buttons;
    [SerializeField] private KMSelectable _submitButton;
    [SerializeField] private KMSelectable _symbolDisplaySelectable;
    [SerializeField] private Animator _submitButtonAnimator;

    [HideInInspector] public KMBombInfo Bomb;
    [HideInInspector] public KMAudio Audio;
    [HideInInspector] public KMBombModule Module;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    // Display happy face should really be a method in MiniScreen.cs but oh well.
    public readonly string[] HappyFaces = new string[] {
        ":)",
        ": )",
        ":-)",
        "=)",
        "= )",
        "=-)",
        ":]" ,
        ": ]",
        ":-]",
        "=]",
        "= ]",
        "=-]"
    };
    private readonly string[] _strikeFaces = new string[] {
        ">:(",
        ">:[",
        ">:<",
        ":'(",
        ">:x",
        ":|",
        ">:|",
        ":s",
        ":o",
        ":0",
        ":O"
    };
    private readonly string[] _strikeSounds = new string[] {
        "Strike 1",
        "Strike 2",
        "Strike 3"
    };

    private State[] _quirkStates;

    private SamuelSequenceGenerator _sequenceGenerator;
    private SamuelSequenceModifier _sequenceModifier;
    private State _state;

    private bool _stateChanged;

    public ColouredButton[] Buttons { get { return _buttons; } }
    public MainScreen Screen { get; private set; }
    public MiniScreen SymbolDisplay { get; private set; }
    public Animator SubmitButtonAnimator { get { return _submitButtonAnimator; } }

    public ColouredSymbol[] DisplayedSequence { get; private set; }
    public ColouredSymbol ExpectedSubmission { get; private set; }
    public List<ButtonColour> SubmittedColours { get; private set; }
    public List<int> QuirkAppearances { get; private set; }
    public int StageNumber { get; private set; }

    private void Awake() {
        _moduleId = _moduleIdCounter++;

        Bomb = GetComponent<KMBombInfo>();
        Audio = GetComponent<KMAudio>();
        Module = GetComponent<KMBombModule>();
        Screen = GetComponentInChildren<MainScreen>();
        SymbolDisplay = GetComponentInChildren<MiniScreen>();
    }

    private void Start() {
        if (GetComponent<KMColorblindMode>().ColorblindModeActive) {
            Screen.ToggleColourblindMode();
            SymbolDisplay.ToggleColourblindMode();
        }

        _sequenceGenerator = new SamuelSequenceGenerator();
        _sequenceModifier = new SamuelSequenceModifier(this);
        SubmittedColours = new List<ButtonColour>();
        QuirkAppearances = new List<int>();
        AssignInputHandlers();

        Log("Samuel says hi!");
        Log("Press the grey button to start.");

        _quirkStates = new State[] {
            new DiscoloredQuirk(this),
            new GhostQuirk(this),
            new OverclockedQuirk(this),
            new StuckQuirk(this),
            new StutterQuirk(this),
            new UnstableQuirk(this),
            new VirusQuirk(this)
        };

        StageNumber = 0;
        _state = new InitialState(this);
    }

    private void AssignInputHandlers() {
        int count = 0;

        foreach (ColouredButton button in _buttons) {
            button.Selectable.OnInteract += delegate () { StartCoroutine(_state.HandlePress(button)); return false; };
            button.Selectable.OnInteractEnded += delegate () { StartCoroutine(_state.HandleRelease(button)); };
            button.SetColour((ButtonColour)count++);
        }

        _submitButton.OnInteract += delegate () { StartCoroutine(_state.HandleSubmitPress()); return false; };
        _submitButton.OnInteractEnded += delegate () { StartCoroutine(_state.HandleSubmitRelease()); };
        _symbolDisplaySelectable.OnInteract += delegate () { Screen.ToggleColourblindMode(); SymbolDisplay.ToggleColourblindMode(); return false; };
    }

    public void ChangeState(State newState, bool haltSequence = true) {
        _stateChanged = true;
        if (haltSequence) {
            Screen.StopSequence();
        }
        SymbolDisplay.ClearScreen();
        _state = newState;
        StartCoroutine(_state.OnStateEnter());
    }

    public void Strike(string loggingMessage, string strikeSound = "") {
        Log("✕ " + loggingMessage);
        StartCoroutine(FlashStrikeFace());
        if (strikeSound == "") {
            Audio.PlaySoundAtTransform(_strikeSounds[Rnd.Range(0, _strikeSounds.Length)], transform);
        }
        else {
            Audio.PlaySoundAtTransform(strikeSound, transform);
        }
        Module.HandleStrike();
    }

    private IEnumerator FlashStrikeFace() {
        SymbolDisplay.DisplayEmoticon(_strikeFaces[Rnd.Range(0, _strikeFaces.Length)], Color.red);
        _stateChanged = false;
        Screen.PauseSequence();
        yield return new WaitForSeconds(1f);
        if (!_stateChanged) {
            SymbolDisplay.ClearScreen();
            Screen.UnpauseSequence();
        }
    }

    public void Log(string formattedString) {
        Debug.LogFormat("[Samuel Says #{0}] {1}", _moduleId, formattedString);
    }

    public void Log(List<string> formattedStrings) {
        formattedStrings.ForEach(str => Log(str));
    }


    public void AdvanceStage() {
        StageNumber++;

        if (StageNumber == 5) {
            ChangeState(new LeftToRightAnimation(this, new FinalStage(this)));
            return;
        }

        DisplayedSequence = _sequenceGenerator.GenerateRandomSequence(3 + Rnd.Range(0, 2));
        ExpectedSubmission = _sequenceModifier.GetExpectedSubmission(DisplayedSequence);
        SubmittedColours.Add(ExpectedSubmission.Colour);

        if (StageNumber == 1) {
            ChangeState(new RegularStage(this));
        }
        else if (Rnd.Range(0, 2) == 1) {
            int quirkNumber = Rnd.Range(0, _quirkStates.Length);
            QuirkAppearances.Add(quirkNumber);
            ChangeState(new LeftToRightAnimation(this, _quirkStates[quirkNumber]));
        }
        else {
            ChangeState(new LeftToRightAnimation(this, new RegularStage(this)));
        }
    }

    public void DoStageLogging() {
        string sequenceAsString = string.Join(", ", DisplayedSequence.Select(c => c.ToString()).ToArray());
        Log("================== Stage " + StageNumber + " ==================");
        Log("The displayed sequence is " + sequenceAsString + ".");
        Log(_sequenceModifier.SequenceGenerationLogging);
        Log("The expected response is " + ExpectedSubmission.ToString() + ".");
    }

    public void LogQuirk(string quirkName) {
        Log(">>>>> Quirk: " + quirkName + " <<<<<");
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use '!{0} start/mute/gray' to press the gray button; '!{0} <colorblind/cb>' | "
        + "Stages 1-4: '!{0} <transmit/tx> <color> <symbol>' to transmit a colored symbol | "
        + "Quirks: '!{0} press <colors/positions>'; '!{0} touch <color/position> <last seconds digit>; '!{0} spam' to calm Samuel down | "
        + "Stage 5: '!{0} <transmit/tx> <morse> <colors>' (eg '!{0} tx ....---..-..-.. RY' submits 'SAMUEL', alternating red and yellow) | "
        + "Colors are R/Y/G/B; positions are 1-4 in reading order; symbols are . (dot) or - (dash).";
#pragma warning restore 414

    string[] colourAbbreviations = new string[] { "r", "y", "g", "b" };

    private IEnumerator ProcessTwitchCommand(string command) {
        if (_state.GetType() == typeof(LeftToRightAnimation)) {
            yield return "sendtochaterror Wait for the animation to finish, then try again.";
        }

        command = command.Trim().ToLower();
        yield return null;

        if (command == "start" || command == "mute" || command == "gray") {
            _submitButton.OnInteract();
            yield return new WaitForSeconds(0.1f);
            _submitButton.OnInteractEnded();
            yield break;
        }
        else if (command == "colorblind" || command == "cb") {
            _symbolDisplaySelectable.OnInteract();
            yield break;
        }
        else if (command == "spam") {
            for (int i = 0; i < 10; i++) {
                int position = Rnd.Range(0, 4);
                Buttons[position].Selectable.OnInteract();
                yield return new WaitForSeconds(1 / 14f);
                Buttons[position].Selectable.OnInteractEnded();
                yield return new WaitForSeconds(1 / 14f);
            }
            yield break;
        }
        else if (command == string.Empty) {
            yield return "sendtochaterror That's an empty command...";
            yield break;
        }

        string[] commandList = command.Split(' ');

        if (commandList.Length == 3 && (commandList[0] == "transmit" || commandList[0] == "tx")) {
            if (colourAbbreviations.Contains(commandList[1])) {
                if (commandList[2] == ".") {
                    Buttons[Array.IndexOf(colourAbbreviations, commandList[1])].Selectable.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    Buttons[Array.IndexOf(colourAbbreviations, commandList[1])].Selectable.OnInteractEnded();
                }
                else if (commandList[2] == "-") {
                    Buttons[Array.IndexOf(colourAbbreviations, commandList[1])].Selectable.OnInteract();
                    yield return new WaitForSeconds(0.31f);
                    Buttons[Array.IndexOf(colourAbbreviations, commandList[1])].Selectable.OnInteractEnded();
                }
                else {
                    yield return "sendtochaterror Invalid symbol!";
                }
            }
            else if (!commandList[1].Any(symbol => symbol != '.' && symbol != '-')) {
                if (!commandList[2].Any(color => !colourAbbreviations.Contains(color.ToString()))) {
                    int counter = 0;
                    int loopLength = commandList[2].Length;

                    foreach (char symbol in commandList[1]) {
                        Buttons[Array.IndexOf(colourAbbreviations, commandList[2][counter % loopLength].ToString())].Selectable.OnInteract();
                        if (symbol == '.') {
                            yield return new WaitForSeconds(0.1f);
                        }
                        else {
                            yield return new WaitForSeconds(0.31f);
                        }
                        Buttons[Array.IndexOf(colourAbbreviations, commandList[2][counter % loopLength].ToString())].Selectable.OnInteractEnded();
                        counter++;
                        yield return new WaitForSeconds(0.1f);
                    }
                }
                else {
                    yield return "sendtochaterror Invalid color sequence!";
                }
            }
            else {
                yield return "sendtocharerror Invalid command!";
            }
        }
        else if (commandList.Length == 2 && commandList[0] == "press") {
            int counter = 0;
            foreach (char symbol in commandList[1]) {
                int position = colourAbbreviations.Contains(symbol.ToString()) ? Array.IndexOf(colourAbbreviations, symbol.ToString()) : symbol - '1';
                counter++;
                if (position >= 0 && position <= 3) {
                    Buttons[position].Selectable.OnInteract();
                    yield return new WaitForSeconds(0.1f);
                    Buttons[position].Selectable.OnInteractEnded();
                    yield return new WaitForSeconds(0.1f);
                }
                else {
                    yield return "sendtochaterror Invalid position or color! Halted just before press number " + counter + ".";
                    yield break;
                }
            }
        }
        else if (commandList.Length == 3 && commandList[0] == "touch") {
            int position;
            int time;
            if (!int.TryParse(commandList[1], out position)) {
                position = Array.IndexOf(colourAbbreviations, commandList[1]) + 1;
            }
            if (int.TryParse(commandList[2], out time) && time >= 0 && time <= 9 && position >= 1 && position <= 4) {
                while (Math.Floor(Bomb.GetTime()) % 10 != time) {
                    yield return new WaitForSeconds(0.1f);
                }
                Buttons[position - 1].Selectable.OnInteract();
                Buttons[position - 1].Selectable.OnInteractEnded();
            }
            else {
                yield return "sendtochaterror Invalid time, position, or color!";
            }
        }
        else {
            yield return "sendtochaterror Invalid command!";
        }
    }

    private IEnumerator TwitchHandleForcedSolve() {
        yield return null;

        TpAction nextAction = _state.NextTpAction();

        while (nextAction.ActionType != TpActionType.Solved) {
            if (nextAction.ActionType == TpActionType.Wait) {
                yield return true;
            }
            else if (nextAction.ActionType == TpActionType.Start) {
                _submitButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
                _submitButton.OnInteractEnded();
            }
            else {
                if (nextAction.ActionType == TpActionType.PressStuck) {
                    while ((Math.Floor(Bomb.GetTime()) - Bomb.GetSolvedModuleNames().Count()) % 10 != 0) {
                        yield return new WaitForSeconds(0.1f);
                    }
                    Buttons[nextAction.Position].Selectable.OnInteract();
                    Buttons[nextAction.Position].Selectable.OnInteractEnded();
                }
                else {
                    Buttons[nextAction.Position].Selectable.OnInteract();
                    switch (nextAction.ActionType) {
                    case TpActionType.PressLong:
                        yield return new WaitForSeconds(0.31f);
                        break;
                    case TpActionType.PressShort:
                        // yield return new WaitForSeconds(0.1f);
                        // the big lag the big sad
                        break;
                    default:
                        throw new ArgumentException("Idk how but invalid TpActionType.");
                    }
                    Buttons[nextAction.Position].Selectable.OnInteractEnded();
                }
                yield return new WaitForSeconds(0.05f);
            }
            nextAction = _state.NextTpAction();
        }
    }
}
