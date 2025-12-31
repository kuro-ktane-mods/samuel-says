using System;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using UnityEngine;

public class SamuelSequenceModifier {

    // Only need 3/4 symbols long.
    private readonly Dictionary<int, string> MorseLetters = new Dictionary<int, string>() {
        {1, "-..."},
        {2, "-.-."},
        {3, "-.."},
        {5, "..-."},
        {6, "--."},
        {7, "...."},
        {9, ".---"},
        {10, "-.-"},
        {11, ".-.."},
        {14, "---"},
        {15, ".--."},
        {16, "--.-"},
        {17, ".-."},
        {18, "..."},
        {19, "..-"},
        {21, "...-"},
        {22, ".--"},
        {23, "-..-"},
        {24, "-.--"},
        {25, "--.."}
    };
    private Dictionary<ButtonColour, ButtonColour> _redAction4ColourSwaps;

    private SamuelSaysModule _module;

    // Permament values.
    private bool _moduleWithRedInName;
    private bool _shoutsOrSendsPresent;
    private int _batteryCount;
    private int _totalPorts;
    private int _uniquePortTypeCount;
    private int _litIndicatorCount;
    private int _unlitIndicatorCount;
    private int _serialNumberDigitSum;
    private int _moduleCount;

    // Variable values.
    private bool _blueHasBeenInPositionThreeThisStage = false;
    private bool _greenHasAppearedBefore = false;
    private bool _moreThanOneYellowInCurrentDisplay = false;
    private bool _redHasFailedToAppearInDisplay = false;

    private Func<bool>[][] _conditions;
    private Func<bool>[] _redConditions;
    private Func<bool>[] _yellowConditions;
    private Func<bool>[] _greenConditions;
    private Func<bool>[] _blueConditions;

    private Action[][] _actions;
    private Action[] _redActions;
    private Action[] _yellowActions;
    private Action[] _greenActions;
    private Action[] _blueActions;

    private string _displayedSymbols;
    private string _modifiedSymbols;
    private List<ButtonColour> _displayedColours = new List<ButtonColour>();
    private List<ButtonColour> _modifiedColours = new List<ButtonColour>();

    private List<int> _appliedActionsThisStage = new List<int>();
    private List<string> _sequenceGenerationLogging = new List<string>();

    public SamuelSequenceModifier(SamuelSaysModule module) {
        _module = module;
        SetPermanentValues();
        SetConditions();
        SetActions();
    }

    public List<string> SequenceGenerationLogging { get { return _sequenceGenerationLogging; } }

    private void SetPermanentValues() {
        KMBombInfo bomb = _module.Bomb;
        List<string> moduleNames = bomb.GetModuleNames();

        _moduleWithRedInName = moduleNames.Any(modName => modName.ToLower().Contains("red"));
        _shoutsOrSendsPresent = moduleNames.Contains("Simon Shouts") || moduleNames.Contains("Simon Sends");
        _batteryCount = bomb.GetBatteryCount();
        _totalPorts = bomb.GetPorts().Count();
        _uniquePortTypeCount = bomb.CountUniquePorts();
        _litIndicatorCount = bomb.GetOnIndicators().Count();
        _unlitIndicatorCount = bomb.GetOffIndicators().Count();
        _serialNumberDigitSum = bomb.GetSerialNumberNumbers().Sum();
        _moduleCount = bomb.GetModuleNames().Count();
    }

    private void SetConditions() {
        // * Conditions are in reading order in the corresponding table.
        _redConditions = new Func<bool>[] {
            delegate() {return _displayedSymbols == ".-.";},
            delegate() {return !_redHasFailedToAppearInDisplay;},
            delegate() {return _modifiedSymbols.Count(symbol => symbol == '-') == _litIndicatorCount + _unlitIndicatorCount;},
            delegate() {return _moduleWithRedInName;},
            delegate() {return true;}
        };

        _yellowConditions = new Func<bool>[] {
            delegate() {return _displayedSymbols == "-.--";},
            delegate() {return _module.StageNumber == _batteryCount;},
            delegate() {return _shoutsOrSendsPresent;},
            delegate() {return _moreThanOneYellowInCurrentDisplay;},
            delegate() {return true;}
        };

        _greenConditions = new Func<bool>[] {
            delegate() {return _displayedSymbols == "--.";},
            delegate() {return _displayedColours[1] == ButtonColour.Green;},
            delegate() {return !_greenHasAppearedBefore;},
            delegate() {return _modifiedSymbols.Count(symbol => symbol == '.') == _uniquePortTypeCount;},
            delegate() {return true;}
        };

        _blueConditions = new Func<bool>[] {
            delegate() {return _displayedSymbols == "-...";},
            delegate() {return _blueHasBeenInPositionThreeThisStage;},
            delegate() {return !MorseLetters.ContainsValue(_displayedSymbols);},
            delegate() {return _displayedColours.Concat(_modifiedColours).Distinct().Count() == 4;},
            delegate() {return true;}
        };

        _conditions = new Func<bool>[][] { _redConditions, _yellowConditions, _greenConditions, _blueConditions };
    }

    private void SetActions() {
        _redAction4ColourSwaps = new Dictionary<ButtonColour, ButtonColour>() {
            {ButtonColour.Red, ButtonColour.Blue},
            {ButtonColour.Blue, ButtonColour.Red},
            {ButtonColour.Yellow, ButtonColour.Green},
            {ButtonColour.Green, ButtonColour.Yellow}
        };

        // * Actions are in reading order in the corresponding table.
        _redActions = new Action[] {
            delegate() {_modifiedSymbols = _modifiedSymbols.Replace('-', '1').Replace('.', '-').Replace('1', '.');},
            delegate() {_modifiedColours = _modifiedColours.Select(colour => ButtonColour.Red).ToList();},
            delegate() {ShiftByLitMinusUnlitIndicators();},
            delegate() {_modifiedColours = _modifiedColours.Select(colour => _redAction4ColourSwaps[colour]).ToList();},
            delegate() {SetPositionsOneTwoToThreeFour();}
        };

        _yellowActions = new Action[] {
            delegate() {_modifiedSymbols = new string(_modifiedSymbols.Reverse().ToArray()); _modifiedColours.Reverse();},
            delegate() {if (_modifiedSymbols.Length == 4) RemovePositionN(); else InsertYellowDashAtPositionM();},
            delegate() {_modifiedColours[0] = ButtonColour.Yellow; _modifiedColours[1] = ButtonColour.Yellow;},
            delegate() {_modifiedColours = _modifiedColours.Select((colour, index) => index == 2 ? colour : ButtonColour.Blue).ToList();},
            delegate() {_modifiedColours = ShiftRight(_modifiedColours, 2);}
        };

        _greenActions = new Action[] {
            delegate() {DashesGreenDotsToDashes();},
            delegate() {SetColoursRbygOrder();},
            delegate() {_greenHasAppearedBefore = true; _modifiedColours = _modifiedColours.Select((c, i) => i == 0 ? ButtonColour.Green : ButtonColour.Red).ToList();},
            delegate() {MoveDotsToFront();},
            delegate() {RemovePositionTwoAndAppendGreenDot();}
        };

        _blueActions = new Action[] {
            delegate() {_modifiedSymbols = new string(_modifiedSymbols.Reverse().ToArray());},
            delegate() {_modifiedSymbols = ShiftRight(_modifiedSymbols, 1); _modifiedColours = ShiftRight(_modifiedColours, 1);},
            delegate() {SetSymbolsToFirstValidMorseLetter();},
            delegate() {/* Do nothing <3 */},
            delegate() {_modifiedColours = _modifiedColours.Select((colour, index) => index < 2 ? ButtonColour.Blue : ButtonColour.Red).ToList();},
        };

        _actions = new Action[][] { _redActions, _yellowActions, _greenActions, _blueActions };
    }

    public ColouredSymbol GetExpectedSubmission(ColouredSymbol[] displayedSequence) {
        DeconstructDisplayedSequence(displayedSequence);
        _modifiedSymbols = _displayedSymbols;
        _modifiedColours = _displayedColours.ToList();
        _appliedActionsThisStage.Clear();
        _sequenceGenerationLogging.Clear();

        _displayedColours.ForEach(colour => ModifySequence(colour));
        ColouredSymbol[] modifiedSequence = ConstructModifiedSequence(_modifiedSymbols, _modifiedColours);

        return modifiedSequence[GetCorrectPosition()];
    }

    private void ModifySequence(ButtonColour currentSymbolColour) {
        CheckBlueInPositionThree();
        Func<bool>[] conditions = _conditions[(int)currentSymbolColour];
        Action[] actions = _actions[(int)currentSymbolColour];
        int activeCondition = 0;
        int activeAction;

        while (!conditions[activeCondition]()) {
            activeCondition++;
        }
        _sequenceGenerationLogging.Add(currentSymbolColour + ": Condition " + (activeCondition + 1) + " applies.");

        // If you reach a rule that you have already applied, use the previous rule instead.
        activeAction = activeCondition;
        while (_appliedActionsThisStage.Contains((int)currentSymbolColour * 5 + activeAction)) {
            _sequenceGenerationLogging.Add("Action " + (activeAction + 1) + " has already been applied.");
            // Add 4 instead of subtracting 1 to avoid negative result after modulo.
            activeAction += 4;
            activeAction %= 5;
        }
        _sequenceGenerationLogging.Add("Applying action " + (activeAction + 1) + ".");
        _appliedActionsThisStage.Add((int)currentSymbolColour * 5 + activeAction);

        actions[activeAction]();
    }

    private void CheckBlueInPositionThree() {
        if (!_blueHasBeenInPositionThreeThisStage) {
            if (_modifiedColours.Count() >= 3 && _modifiedColours[2] == ButtonColour.Blue) {
                _blueHasBeenInPositionThreeThisStage = true;
            }
        }
    }

    private int GetCorrectPosition() {
        int quantityToUse;
        int position;
        string usingQuantity;

        switch (_module.StageNumber) {
            case 1: quantityToUse = _batteryCount; usingQuantity = "batterie"; break;
            case 2: quantityToUse = _totalPorts; usingQuantity = "port"; break;
            case 3: quantityToUse = _litIndicatorCount + _unlitIndicatorCount; usingQuantity = "indicator"; break;
            case 4: quantityToUse = _moduleCount; usingQuantity = "module"; break;
            default: throw new ArgumentOutOfRangeException("WTF STAGE NUMBER ARE WE ON :(");
        }

        position = quantityToUse % _modifiedSymbols.Length;
        if (quantityToUse != 1) {
            _sequenceGenerationLogging.Add("There are " + quantityToUse + " " + usingQuantity + "s, so the correct position to submit is " + (position + 1) + ".");
        }
        else {
            if (usingQuantity == "batterie") {
                usingQuantity = "battery";
            }
            _sequenceGenerationLogging.Add("There is " + quantityToUse + " " + usingQuantity + ", so the correct position to submit is " + (position + 1) + ".");
        }

        return position;
    }

    private void DeconstructDisplayedSequence(ColouredSymbol[] sequence) {
        _displayedSymbols = string.Empty;
        _displayedColours.Clear();

        foreach (ColouredSymbol symbol in sequence) {
            _displayedSymbols += symbol.Symbol;
            _displayedColours.Add(symbol.Colour);
        }

        if (!_displayedColours.Contains(ButtonColour.Red)) {
            _redHasFailedToAppearInDisplay = true;
        }

        _moreThanOneYellowInCurrentDisplay = _displayedColours.Count(colour => colour == ButtonColour.Yellow) >= 2;
        _blueHasBeenInPositionThreeThisStage = false;

    }

    private ColouredSymbol[] ConstructModifiedSequence(string symbols, List<ButtonColour> colours) {
        if (symbols.Length != colours.Count()) {
            Debug.Log("Symbol count is " + symbols.Length + ". Colour count is " + colours.Count());
            Debug.Log("Displayed sequence is are: ");
            foreach (ColouredSymbol symbol in _module.DisplayedSequence) {
                Debug.Log(symbol.Colour + " " + symbol.Symbol);
            }
            throw new RankException("Number of symbols and number of colours must match to construct new sequence.");
        }

        ColouredSymbol[] newSequence = new ColouredSymbol[symbols.Length];

        for (int i = 0; i < symbols.Length; i++) {
            newSequence[i] = new ColouredSymbol(colours[i], symbols[i]);
        }

        return newSequence;
    }

    // Methods used for actions:
    private string ShiftRight(string text, int offset) {
        offset %= text.Length;

        if (offset == 0) {
            return text;
        }
        else if (offset < 0) {
            offset += text.Length;
        }

        return text.Substring(text.Length - offset) + text.Substring(0, text.Length - offset);
    }

    private List<T> ShiftRight<T>(List<T> list, int offset) {
        int length = list.Count();
        int startIndex = (length - offset % length) % length;
        var shiftedList = new List<T>();

        for (int i = 0; i < length; i++) {
            shiftedList.Add(list[(startIndex + i) % length]);
        }

        return shiftedList;
    }

    private void ShiftByLitMinusUnlitIndicators() {
        int shiftIndex = _litIndicatorCount - _unlitIndicatorCount;
        _modifiedSymbols = ShiftRight(_modifiedSymbols, shiftIndex);
        _modifiedColours = ShiftRight(_modifiedColours, shiftIndex);
    }

    private void SetPositionsOneTwoToThreeFour() {
        _modifiedSymbols = _modifiedSymbols.Insert(0, _modifiedSymbols[2].ToString()).Remove(1, 1);
        _modifiedColours.RemoveAt(0);
        _modifiedColours.Insert(0, _modifiedColours[1]);
        if (_modifiedSymbols.Length == 4) {
            _modifiedSymbols = _modifiedSymbols.Insert(1, _modifiedSymbols[3].ToString()).Remove(2, 1);
            _modifiedColours.RemoveAt(1);
            _modifiedColours.Insert(1, _modifiedColours[2]);
        }
    }

    private void RemovePositionN() {
        int n = 4 - (_batteryCount % 4);
        _modifiedColours.RemoveAt(n - 1);
        _modifiedSymbols = _modifiedSymbols.Remove(startIndex: n - 1, count: 1);
    }

    private void InsertYellowDashAtPositionM() {
        int m = _serialNumberDigitSum % 10 % 3;
        _modifiedColours.Insert(m, ButtonColour.Yellow);
        _modifiedSymbols = _modifiedSymbols.Insert(m, "-");
    }

    private void DashesGreenDotsToDashes() {
        for (int i = 0; i < _modifiedSymbols.Length; i++) {
            if (_modifiedSymbols[i] == '-') {
                _modifiedColours[i] = ButtonColour.Green;
            }
            else {
                _modifiedSymbols = _modifiedSymbols.Remove(startIndex: i, count: 1);
                _modifiedSymbols = _modifiedSymbols.Insert(startIndex: i, value: "-");
            }
        }
    }

    private void SetColoursRbygOrder() {
        _modifiedColours = new List<ButtonColour> {
            ButtonColour.Red,
            ButtonColour.Blue,
            ButtonColour.Yellow
        };
        if (_modifiedSymbols.Length == 4) {
            _modifiedColours.Add(ButtonColour.Green);
        }
    }

    private void MoveDotsToFront() {
        int numDots = _modifiedSymbols.Count(s => s == '.');
        _modifiedSymbols = new string('.', numDots);
        while (_modifiedSymbols.Length < _modifiedColours.Count()) {
            _modifiedSymbols += "-";
        }
    }

    private void RemovePositionTwoAndAppendGreenDot() {
        _modifiedSymbols = _modifiedSymbols.Remove(startIndex: 1, count: 1);
        _modifiedSymbols += ".";
        _modifiedColours.RemoveAt(1);
        _modifiedColours.Add(ButtonColour.Green);
    }

    private void SetSymbolsToFirstValidMorseLetter() {
        int tryNumber = _serialNumberDigitSum % 26;
        while (!MorseLetters.ContainsKey(tryNumber) || MorseLetters[tryNumber].Length != _modifiedSymbols.Length) {
            tryNumber++;
            tryNumber %= 26;
        }
        _modifiedSymbols = MorseLetters[tryNumber];
    }
}
