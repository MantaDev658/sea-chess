namespace ChessPlatform.Domain

// ─────────────────────────────────────────────────────────
//  FenParser.fs — Pure FEN ↔ GameState conversion.
//  No side effects, no external dependencies.
// ─────────────────────────────────────────────────────────

module FenParser =

    /// Map a FEN piece character to a Piece value.
    let private charToPiece (c: char) : Piece option =
        match c with
        | 'K' -> Some { Color = White; Type = King }
        | 'Q' -> Some { Color = White; Type = Queen }
        | 'R' -> Some { Color = White; Type = Rook }
        | 'B' -> Some { Color = White; Type = Bishop }
        | 'N' -> Some { Color = White; Type = Knight }
        | 'P' -> Some { Color = White; Type = Pawn }
        | 'k' -> Some { Color = Black; Type = King }
        | 'q' -> Some { Color = Black; Type = Queen }
        | 'r' -> Some { Color = Black; Type = Rook }
        | 'b' -> Some { Color = Black; Type = Bishop }
        | 'n' -> Some { Color = Black; Type = Knight }
        | 'p' -> Some { Color = Black; Type = Pawn }
        | _ -> None

    /// Map a Piece to its FEN character.
    let private pieceToChar (piece: Piece) : char =
        let c =
            match piece.Type with
            | King -> 'K'
            | Queen -> 'Q'
            | Rook -> 'R'
            | Bishop -> 'B'
            | Knight -> 'N'
            | Pawn -> 'P'

        match piece.Color with
        | White -> c
        | Black -> System.Char.ToLower c

    /// Parse the piece-placement field (rank descriptions separated by '/').
    /// FEN ranks go from rank 8 (index 7) down to rank 1 (index 0).
    let private parsePiecePlacement (placement: string) : Result<Board, string> =
        let ranks = placement.Split('/')

        if ranks.Length <> 8 then
            Error $"Expected 8 ranks in piece placement, got {ranks.Length}"
        else
            let folder (board: Board, file: int) (c: char) =
                if System.Char.IsDigit c then
                    (board, file + (int c - int '0'))
                else
                    match charToPiece c with
                    | Some piece -> (Map.add (file, 0) piece board, file + 1)
                    | None -> (board, file) // skip unknown chars — caught later

            // Process ranks top-to-bottom: FEN rank 0 = board rank 7
            let mutable board = Map.empty
            let mutable error = None

            for rankIdx in 0..7 do
                let boardRank = 7 - rankIdx
                let rankStr = ranks.[rankIdx]
                let mutable file = 0

                for c in rankStr do
                    if System.Char.IsDigit c then
                        file <- file + (int c - int '0')
                    else
                        match charToPiece c with
                        | Some piece ->
                            board <- Map.add (file, boardRank) piece board
                            file <- file + 1
                        | None -> error <- Some $"Unknown piece character: '{c}'"

                if file <> 8 && error.IsNone then
                    error <- Some $"Rank {boardRank + 1} has {file} files instead of 8"

            match error with
            | Some msg -> Error msg
            | None -> Ok board

    /// Parse the active color field.
    let private parseActiveColor (s: string) : Result<Color, string> =
        match s with
        | "w" -> Ok White
        | "b" -> Ok Black
        | _ -> Error $"Invalid active color: '{s}'"

    /// Parse the castling availability field.
    let private parseCastlingRights (s: string) : Result<CastlingRights, string> =
        if s = "-" then
            Ok
                {
                    WhiteKingSide = false
                    WhiteQueenSide = false
                    BlackKingSide = false
                    BlackQueenSide = false
                }
        else
            let valid = s |> Seq.forall (fun c -> "KQkq".Contains(c))

            if not valid then
                Error $"Invalid castling rights: '{s}'"
            else
                Ok
                    {
                        WhiteKingSide = s.Contains('K')
                        WhiteQueenSide = s.Contains('Q')
                        BlackKingSide = s.Contains('k')
                        BlackQueenSide = s.Contains('q')
                    }

    /// Parse the en passant target square field.
    let private parseEnPassant (s: string) : Result<Position option, string> =
        if s = "-" then
            Ok None
        else if s.Length = 2 then
            let file = int s.[0] - int 'a'
            let rank = int s.[1] - int '1'

            if file >= 0 && file <= 7 && rank >= 0 && rank <= 7 then
                Ok(Some(file, rank))
            else
                Error $"En passant target out of range: '{s}'"
        else
            Error $"Invalid en passant target: '{s}'"

    /// Parse a non-negative integer field.
    let private parseInt (label: string) (s: string) : Result<int, string> =
        match System.Int32.TryParse s with
        | true, n when n >= 0 -> Ok n
        | _ -> Error $"Invalid {label}: '{s}'"

    // ───────── Public API ─────────

    /// Parse a standard FEN string into a GameState.
    /// Returns Error for malformed input.
    let parse (fen: string) : Result<GameState, string> =
        let fields = fen.Trim().Split(' ')

        if fields.Length <> 6 then
            Error $"FEN must have 6 space-separated fields, got {fields.Length}"
        else
            // Use Result computation via nested binds
            parsePiecePlacement fields.[0]
            |> Result.bind (fun board ->
                parseActiveColor fields.[1]
                |> Result.bind (fun activeColor ->
                    parseCastlingRights fields.[2]
                    |> Result.bind (fun castling ->
                        parseEnPassant fields.[3]
                        |> Result.bind (fun epTarget ->
                            parseInt "half-move clock" fields.[4]
                            |> Result.bind (fun halfMove ->
                                parseInt "full-move number" fields.[5]
                                |> Result.map (fun fullMove ->
                                    {
                                        Board = board
                                        ActiveColor = activeColor
                                        CastlingRights = castling
                                        EnPassantTarget = epTarget
                                        HalfMoveClock = halfMove
                                        FullMoveNumber = fullMove
                                        History = []
                                        Status = InProgress
                                        Version = 0
                                    }))))))

    /// Convert a Position to algebraic notation (e.g., (0,0) -> "a1").
    let positionToAlgebraic ((file, rank): Position) : string =
        $"{char (int 'a' + file)}{rank + 1}"

    /// Convert algebraic notation to a Position (e.g., "a1" -> (0,0)).
    let algebraicToPosition (s: string) : Position option =
        if s.Length = 2 then
            let file = int s.[0] - int 'a'
            let rank = int s.[1] - int '1'

            if file >= 0 && file <= 7 && rank >= 0 && rank <= 7 then
                Some(file, rank)
            else
                None
        else
            None

    /// Serialize a GameState back to a FEN string.
    let toFen (state: GameState) : string =
        // 1. Piece placement
        let placement =
            [ for rank in 7 .. -1 .. 0 do
                  let mutable empty = 0
                  let mutable rankStr = ""

                  for file in 0..7 do
                      match Map.tryFind (file, rank) state.Board with
                      | Some piece ->
                          if empty > 0 then
                              rankStr <- rankStr + string empty
                              empty <- 0

                          rankStr <- rankStr + string (pieceToChar piece)
                      | None -> empty <- empty + 1

                  if empty > 0 then
                      rankStr <- rankStr + string empty

                  yield rankStr ]
            |> String.concat "/"

        // 2. Active color
        let activeColor =
            match state.ActiveColor with
            | White -> "w"
            | Black -> "b"

        // 3. Castling rights
        let castling =
            let s =
                (if state.CastlingRights.WhiteKingSide then "K" else "")
                + (if state.CastlingRights.WhiteQueenSide then
                       "Q"
                   else
                       "")
                + (if state.CastlingRights.BlackKingSide then "k" else "")
                + (if state.CastlingRights.BlackQueenSide then
                       "q"
                   else
                       "")

            if s = "" then "-" else s

        // 4. En passant target
        let epTarget =
            match state.EnPassantTarget with
            | Some pos -> positionToAlgebraic pos
            | None -> "-"

        // 5-6. Clocks
        $"{placement} {activeColor} {castling} {epTarget} {state.HalfMoveClock} {state.FullMoveNumber}"

    /// The standard starting position FEN.
    let startingFen =
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
