var random = new Random(0);
var game = new Game(4, 250);
for (;;) {
    var round = new Round(game.PlayerCount, random);
    while (round.Winner < 0) {
        round.SortCards();
        Console.WriteLine(round.Board);
        Console.WriteLine(
            $"Player {round.Turn} turn, playables: {string.Join(",", round.Playables())}, cards: {string.Join(",", round.CurrentPlayer.Hand)}");
        string line = Console.ReadLine();
        if (line is null or "exit" or "quit") break;
        string err;
        if (line == "pass") {
            int nextPlayer = (round.Turn + 1) % round.Players.Length;
            for (;;) {
                Console.WriteLine(
                    $"Player {nextPlayer} to pass, cards: {string.Join(",", round.Players[nextPlayer].Hand)}");
                string pass = Console.ReadLine();
                err = round.Pass(new Card(pass));
                if (err == null) break;
                Console.WriteLine(err);
            }
        } else if (line.StartsWith("Jo ")) {
            err = round.Play(new Card("Jo"), new Card(line[3..]));
        } else {
            err = round.Play(new Card(line));
        }
        if (err != null) Console.WriteLine(err);
    }
    int playerIndex = 0;
    foreach (var player in round.Players) {
        int score = player.Score(round.Boner);
        game.Scores[playerIndex] += score;
        if (score == 0) {
            Console.WriteLine($"Player {playerIndex} Wins!");
        } else {
            string extra = player.Hand.Any(c => c == round.Boner) ? " Boned!" : "";
            Console.WriteLine($"Player {playerIndex} {string.Join(",", player.Hand)} score: {score}{extra}");
        }
        playerIndex++;
    }
    Console.WriteLine($"Scores: {string.Join(",", game.Scores)}");
    if (game.IsOver) break;
}

public class Game {
    private readonly int _playerCount, _maxScore;
    private readonly int[] _scores;
    public Game(int playerCount, int maxScore) {
        _playerCount = playerCount;
        _maxScore = maxScore;
        _scores = new int[playerCount];
    }
    public int PlayerCount => _playerCount;
    public bool IsOver => _scores.Any(s => s >= _maxScore);
    public int[] Scores => _scores;
}

public class Round {
    private readonly Player[] _players;
    private int _turn;
    private Card _boner = new("Jo");
    private Rank[] max = new Rank[4], min = new Rank[4];

    public Round(int players, Random random) {
        var deck = new Deck().Shuffle(random);
        _players = new Player[players];
        int cardCount = deck.Count / players, bonusCards = deck.Count % players;
        var startCard = new Card("7s");
        for (int i = 0; i < players; i++) {
            _players[i] = new Player(deck, cardCount + (i < bonusCards ? 1 : 0));
            if (_players[i].Hand.Any(card => card == startCard)) _turn = i;
        }
        System.Diagnostics.Contracts.Contract.Assert(deck.Count == 0);
        for (int i = 0; i < 4; i++) {
            min[i] = max[i] = Rank.Joker;
        }
    }

    public string Board {
        get {
            string s = "";
            for (int i = 0; i < 4; i++) {
                Suit suit = (Suit)i;
                if (min[i] == Rank.Joker) {
                    s += $"{char.ToLower(suit.ToString()[0])}:? ";
                } else {
                    s += $"{char.ToLower(suit.ToString()[0])}:";
                    if (min[i] is Rank.Seven && max[i] is Rank.Seven)
                        s += "7";
                    else if (min[i] is Rank.Ace or >= Rank.Ten)
                        s += $"{min[i].ToString()[..1]}-";
                    else
                        s += $"{(int)min[i]}-";
                    if (max[i] is Rank.Ace or >= Rank.Ten)
                        s += $"{max[i].ToString()[..1]} ";
                    else
                        s += $"{(int)max[i]} ";
                }
            }
            s += $"Boner:{_boner}";
            return s;
        }
    }

    public int Turn => _turn;
    public Card Boner => _boner;
    public Player[] Players => _players;
    public Player CurrentPlayer => _players[_turn];

    public int Winner => _players.IndexOf(p => p.Hand.Count == 0);

    public List<Card> Playables() {
        List<Card> playables = new();
        var startCard = new Card("7s");
        if (max[(int)Suit.Spades] == Rank.Joker) {
            playables.Add(_players[_turn].Hand.Single(card => card == startCard));
            playables.AddRange(_players[_turn].Hand.Where(card => card.Rank == Rank.Joker));
        } else {
            Rank minSpade = min[(int)Suit.Spades], maxSpade = max[(int)Suit.Spades];
            foreach (Card card in _players[_turn].Hand) {
                if (card == _boner && _boner.Rank != Rank.Joker) continue;
                if (card.Rank is Rank.Seven or Rank.Joker) {
                    playables.Add(card);
                } else if (max[(int)card.Suit] != Rank.Joker) {
                    if (max[(int)card.Suit] == card.Rank - 1 || min[(int)card.Suit] == card.Rank + 1) {
                        if (card.Suit == Suit.Spades || card.Rank >= minSpade && card.Rank <= maxSpade) {
                            playables.Add(card);
                        }
                    }
                }
            }
        }
        return playables;
    }

    public string Play(Card card, Card jokerPlayAs = null) {
        if (_players[_turn].Hand.All(c => c != card)) return "You don't have that card";
        Rank minSpade = min[(int)Suit.Spades], maxSpade = max[(int)Suit.Spades];
        bool wasJoker = card.Rank == Rank.Joker;
        if (wasJoker) card = jokerPlayAs;
        if (minSpade == Rank.Joker) {
            if (card.Rank != Rank.Seven || card.Suit != Suit.Spades) return "You must play the 7 of Spades";
        }
        if (card.Rank == Rank.Seven && min[(int)card.Suit] == Rank.Joker) {
            min[(int)card.Suit] = max[(int)card.Suit] = card.Rank;
        } else if (card.Rank == min[(int)card.Suit] - 1) {
            if (card.Suit != Suit.Spades && card.Rank < minSpade)
                return "Hasn't been broken yet";
            min[(int)card.Suit] = card.Rank;
        } else if (card.Rank == max[(int)card.Suit] + 1) {
            if (card.Suit != Suit.Spades && card.Rank > maxSpade)
                return "Hasn't been broken yet";
            max[(int)card.Suit] = card.Rank;
        } else {
            return "You can't play that card";
        }
        _players[_turn].Use(wasJoker ? _boner : card);
        if (wasJoker) _boner = jokerPlayAs;
        NextTurn();
        return null;
    }

    public string Pass(Card card) {
        int nextPlayer = (_turn + 1) % _players.Length;
        if (_players[nextPlayer].Hand.All(c => c != card)) return "You don't have that card";
        _players[_turn].Hand.Add(card);
        NextTurn();
        _players[_turn].Use(card);
        return null;
    }

    private void NextTurn() {
        _turn = ++_turn % _players.Length;
    }

    public void SortCards() {
        _players[_turn].SortHand(_boner);
    }
}

public class Deck {
    private readonly List<Card> _cards = new(53);

    public Deck() {
        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
            if (suit != Suit.None)
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    if (rank != Rank.Joker)
                        _cards.Add(new Card(suit, rank));
        _cards.Add(new Card(Suit.None, Rank.Joker));
    }

    public int Count => _cards.Count;

    public Deck Shuffle(Random random) { // Fisher-Yates shuffle
        for (int i = _cards.Count - 1; i > 0; i--) {
            int j = random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
        return this;
    }

    public Card Draw() {
        Card card = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }
}

public class Card {
    public readonly Suit Suit;
    public readonly Rank Rank;

    public bool IsJoker => Rank is Rank.Joker;

    public Card(Suit suit, Rank rank) {
        Suit = suit;
        Rank = rank;
    }

    public Card(string str) {
        if (str[0] == 'J' && str[1] == 'o') {
            Suit = Suit.None;
            Rank = Rank.Joker;
        } else {
            if (char.IsDigit(str[0]))
                Rank = (Rank)(str[0] - '0');
            else
                Rank = Enum.GetValues<Rank>().Last(r => r != Rank.Joker && r.ToString()[0] == str[0]);
            char f = char.ToUpper(str[1]);
            Suit = Enum.GetValues<Suit>().First(s => s != Suit.None && s.ToString()[0] == f);
        }
    }

    public override string ToString() => !IsJoker
        ? $"{(Rank is Rank.Ace or >= Rank.Ten ? Rank.ToString()[..1] : ((int)Rank).ToString())}{char.ToLower(Suit.ToString()[0])}"
        : "Jo";

    public int Value => Rank >= Rank.Ten ? 10 : 5;

    public override bool Equals(object obj) {
        if (obj is not Card card) return false;
        return card.Rank == Rank && card.Suit == Suit;
    }

    public override int GetHashCode() {
        return (int)Rank * 10 + (int)Suit;
    }

    public static bool operator ==(Card a, Card b) {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool operator !=(Card a, Card b) {
        if (a is null && b is null) return false;
        if (a is null || b is null) return true;
        return !a.Equals(b);
    }
}

public enum Rank {
    Joker = 0,
    Ace,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
}

public enum Suit {
    None = -1, // only used for Joker
    Spades,
    Hearts,
    Clubs,
    Diamonds,
}

public class Player {
    public readonly List<Card> Hand;

    public Player(Deck deck, int cards) {
        Hand = new(cards);
        for (int card = 0; card < cards; card++)
            Hand.Add(deck.Draw());
    }

    public Card Use(Card card) {
        if (Hand.RemoveAll(c => c == card) != 1)
            throw new ArgumentException($"Card {card} not found in hand");
        return card;
    }
    
    public void SortHand(Card boner) {
        Hand.Sort((a, b) => {
            if (a == boner) return 1;
            if (b == boner) return -1;
            if (a.Suit != b.Suit) return a.Suit - b.Suit;
            return a.Rank - b.Rank;
        });
    }

    public int Score(Card gameBoner) {
        int score = 0;
        foreach (var card in Hand)
            score += card == gameBoner ? 50 : card.Value;
        return score;
    }
}

public static class Extensions {
    public static int IndexOf<T>(this IEnumerable<T> list, Func<T, bool> predicate) {
        int index = 0;
        foreach (var item in list) {
            if (predicate(item)) return index;
            index++;
        }
        return -1;
    }
}