namespace ChessPlatform.Domain

// ─────────────────────────────────────────────────────────
//  Game.fs — Pure chess rules engine.
//  All functions are pure: GameState → Move → Result<GameState, MoveError>
//  No side effects, no external dependencies.
// ─────────────────────────────────────────────────────────

module Game =

    // ───────── Helpers ─────────

    /// Flip the color.
    let oppositeColor color =
        match color with
        | White -> Black
        | Black -> White

    /// Check if a position is within the 8×8 board.
    let isOnBoard ((file, rank): Position) =
        file >= 0 && file <= 7 && rank >= 0 && rank <= 7

    /// Check if a square is empty.
    let isSquareEmpty (board: Board) (pos: Position) = not (Map.containsKey pos board)

    /// Get the piece at a position, if any.
    let pieceAt (board: Board) (pos: Position) = Map.tryFind pos board

    /// Check if a square is occupied by a piece of the given color.
    let isOccupiedByColor (board: Board) (color: Color) (pos: Position) =
        match pieceAt board pos with
        | Some p -> p.Color = color
        | None -> false

    /// Check if a square is occupied by an enemy piece.
    let isOccupiedByEnemy (board: Board) (color: Color) (pos: Position) =
        isOccupiedByColor board (oppositeColor color) pos

    /// Pawn direction: White moves up (+1 rank), Black moves down (-1 rank).
    let pawnDirection color =
        match color with
        | White -> 1
        | Black -> -1

    /// Starting rank for pawns.
    let pawnStartRank color =
        match color with
        | White -> 1
        | Black -> 6

    /// Promotion rank for pawns (the rank they reach to promote).
    let promotionRank color =
        match color with
        | White -> 7
        | Black -> 0

    /// Find the position of the king of the given color.
    let findKing (board: Board) (color: Color) : Position option =
        board
        |> Map.tryFindKey (fun _ piece -> piece.Color = color && piece.Type = King)

    // ───────── Move Generation (pseudo-legal, before check filtering) ─────────

    /// Generate sliding moves along direction vectors (for Rook, Bishop, Queen).
    let private slidingMoves
        (board: Board)
        (color: Color)
        (from: Position)
        (directions: (int * int) list)
        : Move list =
        [
            for (df, dr) in directions do
                let mutable file, rank = from
                let mutable blocked = false

                while not blocked do
                    file <- file + df
                    rank <- rank + dr
                    let target = (file, rank)

                    if not (isOnBoard target) then
                        blocked <- true
                    else
                        match pieceAt board target with
                        | None -> yield Normal(from, target)
                        | Some piece when piece.Color <> color ->
                            yield Capture(from, target, piece)
                            blocked <- true
                        | Some _ -> blocked <- true
        ]

    /// Generate pawn moves (forward, double-push, captures, en passant, promotion).
    let private pawnMoves
        (board: Board)
        (color: Color)
        (from: Position)
        (epTarget: Position option)
        : Move list =
        let (file, rank) = from
        let dir = pawnDirection color
        let oneForward = (file, rank + dir)
        let twoForward = (file, rank + 2 * dir)
        let promoRank = promotionRank color
        let startRank = pawnStartRank color

        let promotionTypes = [ Queen; Rook; Bishop; Knight ]

        [
            // Single push
            if isOnBoard oneForward && isSquareEmpty board oneForward then
                if snd oneForward = promoRank then
                    for pt in promotionTypes do
                        yield Promotion(from, oneForward, pt)
                else
                    yield Normal(from, oneForward)

                    // Double push from starting rank
                    if rank = startRank && isSquareEmpty board twoForward then
                        yield Normal(from, twoForward)

            // Diagonal captures
            for df in [ -1; 1 ] do
                let captureTarget = (file + df, rank + dir)

                if isOnBoard captureTarget then
                    match pieceAt board captureTarget with
                    | Some piece when piece.Color <> color ->
                        if snd captureTarget = promoRank then
                            for pt in promotionTypes do
                                yield PromotionCapture(from, captureTarget, piece, pt)
                        else
                            yield Capture(from, captureTarget, piece)
                    | _ -> ()

                    // En passant
                    match epTarget with
                    | Some ep when ep = captureTarget ->
                        let capturedPawnPos = (file + df, rank)
                        yield EnPassant(from, captureTarget, capturedPawnPos)
                    | _ -> ()
        ]

    /// Generate knight moves.
    let private knightMoves (board: Board) (color: Color) (from: Position) : Move list =
        let (file, rank) = from

        let offsets =
            [
                (-2, -1)
                (-2, 1)
                (-1, -2)
                (-1, 2)
                (1, -2)
                (1, 2)
                (2, -1)
                (2, 1)
            ]

        [
            for (df, dr) in offsets do
                let target = (file + df, rank + dr)

                if isOnBoard target then
                    match pieceAt board target with
                    | None -> yield Normal(from, target)
                    | Some piece when piece.Color <> color -> yield Capture(from, target, piece)
                    | _ -> ()
        ]

    /// Generate king moves (single step, no castling — castling added separately).
    let private kingBasicMoves (board: Board) (color: Color) (from: Position) : Move list =
        let (file, rank) = from

        let offsets =
            [
                (-1, -1)
                (-1, 0)
                (-1, 1)
                (0, -1)
                (0, 1)
                (1, -1)
                (1, 0)
                (1, 1)
            ]

        [
            for (df, dr) in offsets do
                let target = (file + df, rank + dr)

                if isOnBoard target then
                    match pieceAt board target with
                    | None -> yield Normal(from, target)
                    | Some piece when piece.Color <> color -> yield Capture(from, target, piece)
                    | _ -> ()
        ]

    // ───────── Attack Detection ─────────

    /// Check if a specific square is attacked by any piece of the given color.
    /// Used for check detection and castling validation.
    let isSquareAttackedBy (board: Board) (attackerColor: Color) (targetSquare: Position) : bool =
        let (tf, tr) = targetSquare

        // 1. Pawn attacks
        let pawnDir = pawnDirection attackerColor
        // Pawns of attackerColor at (tf±1, tr-pawnDir) attack targetSquare
        let pawnAttack =
            [ (tf - 1, tr - pawnDir); (tf + 1, tr - pawnDir) ]
            |> List.exists (fun pos ->
                isOnBoard pos
                && (pieceAt board pos = Some { Color = attackerColor; Type = Pawn }))

        if pawnAttack then
            true
        else

        // 2. Knight attacks
        let knightOffsets =
            [
                (-2, -1)
                (-2, 1)
                (-1, -2)
                (-1, 2)
                (1, -2)
                (1, 2)
                (2, -1)
                (2, 1)
            ]

        let knightAttack =
            knightOffsets
            |> List.exists (fun (df, dr) ->
                let pos = (tf + df, tr + dr)

                isOnBoard pos
                && (pieceAt board pos = Some { Color = attackerColor; Type = Knight }))

        if knightAttack then
            true
        else

        // 3. King attacks (adjacent squares)
        let kingOffsets =
            [
                (-1, -1)
                (-1, 0)
                (-1, 1)
                (0, -1)
                (0, 1)
                (1, -1)
                (1, 0)
                (1, 1)
            ]

        let kingAttack =
            kingOffsets
            |> List.exists (fun (df, dr) ->
                let pos = (tf + df, tr + dr)

                isOnBoard pos
                && (pieceAt board pos = Some { Color = attackerColor; Type = King }))

        if kingAttack then
            true
        else

        // 4. Sliding attacks: Rook/Queen along ranks and files
        let rookDirs = [ (1, 0); (-1, 0); (0, 1); (0, -1) ]

        let rookQueenAttack =
            rookDirs
            |> List.exists (fun (df, dr) ->
                let rec scan f r =
                    let nf, nr = f + df, r + dr

                    if not (isOnBoard (nf, nr)) then
                        false
                    else
                        match pieceAt board (nf, nr) with
                        | None -> scan nf nr
                        | Some p ->
                            p.Color = attackerColor
                            && (p.Type = Rook || p.Type = Queen)

                scan tf tr)

        if rookQueenAttack then
            true
        else

        // 5. Sliding attacks: Bishop/Queen along diagonals
        let bishopDirs = [ (1, 1); (1, -1); (-1, 1); (-1, -1) ]

        bishopDirs
        |> List.exists (fun (df, dr) ->
            let rec scan f r =
                let nf, nr = f + df, r + dr

                if not (isOnBoard (nf, nr)) then
                    false
                else
                    match pieceAt board (nf, nr) with
                    | None -> scan nf nr
                    | Some p ->
                        p.Color = attackerColor
                        && (p.Type = Bishop || p.Type = Queen)

            scan tf tr)

    /// Is the king of the given color currently in check?
    let isKingInCheck (board: Board) (color: Color) : bool =
        match findKing board color with
        | Some kingPos -> isSquareAttackedBy board (oppositeColor color) kingPos
        | None -> false // Should never happen in a valid game

    // ───────── Castling ─────────

    /// Generate castling moves if available.
    let private castlingMoves (state: GameState) : Move list =
        let color = state.ActiveColor
        let board = state.Board
        let rank = if color = White then 0 else 7
        let enemy = oppositeColor color

        // King must not be in check
        if isKingInCheck board color then
            []
        else
            [
                // King-side castling
                if
                    (match color with
                     | White -> state.CastlingRights.WhiteKingSide
                     | Black -> state.CastlingRights.BlackKingSide)
                then
                    // Squares between king and rook must be empty
                    let f5 = (5, rank)
                    let f6 = (6, rank)

                    if
                        isSquareEmpty board f5
                        && isSquareEmpty board f6
                        // King must not pass through or land on attacked square
                        && not (isSquareAttackedBy board enemy (4, rank))
                        && not (isSquareAttackedBy board enemy f5)
                        && not (isSquareAttackedBy board enemy f6)
                    then
                        yield Castle(color, KingSide)

                // Queen-side castling
                if
                    (match color with
                     | White -> state.CastlingRights.WhiteQueenSide
                     | Black -> state.CastlingRights.BlackQueenSide)
                then
                    let f3 = (3, rank)
                    let f2 = (2, rank)
                    let f1 = (1, rank)

                    if
                        isSquareEmpty board f3
                        && isSquareEmpty board f2
                        && isSquareEmpty board f1
                        && not (isSquareAttackedBy board enemy (4, rank))
                        && not (isSquareAttackedBy board enemy f3)
                        && not (isSquareAttackedBy board enemy f2)
                    then
                        yield Castle(color, QueenSide)
            ]

    // ───────── Pseudo-Legal Move Generation ─────────

    /// Generate all pseudo-legal moves for a piece at the given position.
    let private pseudoLegalMovesForPiece (state: GameState) (pos: Position) (piece: Piece) : Move list =
        let board = state.Board
        let color = piece.Color

        match piece.Type with
        | Pawn -> pawnMoves board color pos state.EnPassantTarget
        | Knight -> knightMoves board color pos
        | Bishop ->
            let dirs = [ (1, 1); (1, -1); (-1, 1); (-1, -1) ]
            slidingMoves board color pos dirs
        | Rook ->
            let dirs = [ (1, 0); (-1, 0); (0, 1); (0, -1) ]
            slidingMoves board color pos dirs
        | Queen ->
            let dirs =
                [
                    (1, 0)
                    (-1, 0)
                    (0, 1)
                    (0, -1)
                    (1, 1)
                    (1, -1)
                    (-1, 1)
                    (-1, -1)
                ]

            slidingMoves board color pos dirs
        | King -> kingBasicMoves board color pos

    // ───────── Board Manipulation ─────────

    /// Extract the source and destination from any move.
    let moveFromTo (move: Move) : Position * Position =
        match move with
        | Normal(f, t) -> (f, t)
        | Capture(f, t, _) -> (f, t)
        | Castle(color, side) ->
            let rank = if color = White then 0 else 7

            match side with
            | KingSide -> ((4, rank), (6, rank))
            | QueenSide -> ((4, rank), (2, rank))
        | EnPassant(f, t, _) -> (f, t)
        | Promotion(f, t, _) -> (f, t)
        | PromotionCapture(f, t, _, _) -> (f, t)

    /// Apply a move to the board, returning the new board.
    /// Assumes the move has already been validated.
    let applyMoveToBoard (board: Board) (move: Move) : Board =
        match move with
        | Normal(from, to') ->
            let piece = Map.find from board
            board |> Map.remove from |> Map.add to' piece

        | Capture(from, to', _) ->
            let piece = Map.find from board
            board |> Map.remove from |> Map.remove to' |> Map.add to' piece

        | Castle(color, side) ->
            let rank = if color = White then 0 else 7
            let king = Map.find (4, rank) board

            match side with
            | KingSide ->
                let rook = Map.find (7, rank) board

                board
                |> Map.remove (4, rank)
                |> Map.remove (7, rank)
                |> Map.add (6, rank) king
                |> Map.add (5, rank) rook
            | QueenSide ->
                let rook = Map.find (0, rank) board

                board
                |> Map.remove (4, rank)
                |> Map.remove (0, rank)
                |> Map.add (2, rank) king
                |> Map.add (3, rank) rook

        | EnPassant(from, to', capturedPawnPos) ->
            let piece = Map.find from board

            board
            |> Map.remove from
            |> Map.remove capturedPawnPos
            |> Map.add to' piece

        | Promotion(from, to', promoteTo) ->
            let piece = Map.find from board
            let promoted = { piece with Type = promoteTo }
            board |> Map.remove from |> Map.add to' promoted

        | PromotionCapture(from, to', _, promoteTo) ->
            let piece = Map.find from board
            let promoted = { piece with Type = promoteTo }

            board |> Map.remove from |> Map.remove to' |> Map.add to' promoted

    // ───────── Legal Move Filtering ─────────

    /// Filter pseudo-legal moves: remove any that leave the player's own king in check.
    let private filterLegalMoves (board: Board) (color: Color) (moves: Move list) : Move list =
        moves
        |> List.filter (fun move ->
            let newBoard = applyMoveToBoard board move
            not (isKingInCheck newBoard color))

    /// Generate all legal moves for the active player.
    let allLegalMoves (state: GameState) : Move list =
        let color = state.ActiveColor
        let board = state.Board

        let pieceMoves =
            board
            |> Map.toList
            |> List.collect (fun (pos, piece) ->
                if piece.Color = color then
                    pseudoLegalMovesForPiece state pos piece
                else
                    [])

        let castles = castlingMoves state
        let allPseudo = pieceMoves @ castles
        filterLegalMoves board color allPseudo

    // ───────── Castling Rights Update ─────────

    /// Update castling rights based on the move that was just played.
    let private updateCastlingRights
        (rights: CastlingRights)
        (move: Move)
        (board: Board)
        : CastlingRights =
        let (from, to') = moveFromTo move

        // Revoke rights if king moves
        let rights =
            match pieceAt board from with
            | Some { Color = White; Type = King } ->
                { rights with
                    WhiteKingSide = false
                    WhiteQueenSide = false
                }
            | Some { Color = Black; Type = King } ->
                { rights with
                    BlackKingSide = false
                    BlackQueenSide = false
                }
            | _ -> rights

        // Revoke rights if rook moves from its starting square
        let rights =
            match from with
            | (0, 0) -> { rights with WhiteQueenSide = false }
            | (7, 0) -> { rights with WhiteKingSide = false }
            | (0, 7) -> { rights with BlackQueenSide = false }
            | (7, 7) -> { rights with BlackKingSide = false }
            | _ -> rights

        // Revoke rights if rook is captured on its starting square
        let rights =
            match to' with
            | (0, 0) -> { rights with WhiteQueenSide = false }
            | (7, 0) -> { rights with WhiteKingSide = false }
            | (0, 7) -> { rights with BlackQueenSide = false }
            | (7, 7) -> { rights with BlackKingSide = false }
            | _ -> rights

        rights

    // ───────── Game Status Detection ─────────

    /// Detect the game status after a move has been applied.
    let private detectGameStatus (state: GameState) : GameStatus =
        let opponent = state.ActiveColor // After move, active color has flipped
        let legalMoves = allLegalMoves state

        if List.isEmpty legalMoves then
            if isKingInCheck state.Board opponent then
                Checkmate(oppositeColor opponent) // The player who just moved wins
            else
                Stalemate
        else if isKingInCheck state.Board opponent then
            Check opponent
        else if state.HalfMoveClock >= 100 then
            Draw "Fifty-move rule"
        else
            InProgress

    // ───────── Core Public API ─────────

    /// Resolve a MoveRequest into a fully-typed Move by matching it
    /// against the set of legal moves.
    let resolveMove (state: GameState) (request: MoveRequest) : Result<Move, MoveError> =
        // Validate game is not over
        match state.Status with
        | Checkmate _ | Stalemate | Draw _ -> Error GameAlreadyOver
        | _ ->

        // Validate there's a piece at the source
        match pieceAt state.Board request.From with
        | None -> Error NoPieceAtSource
        | Some piece ->

        // Validate the piece belongs to the active player
        if piece.Color <> state.ActiveColor then
            Error WrongColor
        else

        // Find matching legal move
        let legalMoves = allLegalMoves state

        let matchingMove =
            legalMoves
            |> List.tryFind (fun move ->
                let (from, to') = moveFromTo move

                from = request.From
                && to' = request.To
                && (match request.Promotion, move with
                    | Some pt, Promotion(_, _, mpt) -> pt = mpt
                    | Some pt, PromotionCapture(_, _, _, mpt) -> pt = mpt
                    | None, Promotion _ -> false // Must specify promotion type
                    | None, PromotionCapture _ -> false
                    | _ -> true))

        match matchingMove with
        | None -> Error(InvalidMove "No legal move matches the request")
        | Some move -> Ok move

    /// Apply a fully-typed, validated Move to a GameState.
    /// This is the core state transition function.
    let applyMove (state: GameState) (move: Move) : Result<GameState, MoveError> =
        // Validate game is not over
        match state.Status with
        | Checkmate _ | Stalemate | Draw _ -> Error GameAlreadyOver
        | _ ->

        let (from, _to) = moveFromTo move

        // Validate piece exists
        match pieceAt state.Board from with
        | None -> Error NoPieceAtSource
        | Some piece ->

        // Validate color
        if piece.Color <> state.ActiveColor then
            Error WrongColor
        else

        // Apply the move to the board
        let newBoard = applyMoveToBoard state.Board move

        // Verify we haven't left our own king in check
        if isKingInCheck newBoard state.ActiveColor then
            Error MoveLeavesKingInCheck
        else

        // Update castling rights
        let newCastling = updateCastlingRights state.CastlingRights move state.Board

        // Update en passant target
        let newEpTarget =
            match move with
            | Normal(fromPos, toPos) ->
                let piece = Map.find fromPos state.Board

                if
                    piece.Type = Pawn
                    && abs (snd toPos - snd fromPos) = 2
                then
                    // En passant target is the square the pawn skipped
                    Some(fst fromPos, (snd fromPos + snd toPos) / 2)
                else
                    None
            | _ -> None

        // Update half-move clock
        let isCapture =
            match move with
            | Capture _ | EnPassant _ | PromotionCapture _ -> true
            | _ -> false

        let isPawnMove =
            match pieceAt state.Board from with
            | Some p -> p.Type = Pawn
            | None -> false

        let newHalfMoveClock =
            if isPawnMove || isCapture then 0 else state.HalfMoveClock + 1

        // Update full-move number (increments after Black's move)
        let newFullMoveNumber =
            if state.ActiveColor = Black then
                state.FullMoveNumber + 1
            else
                state.FullMoveNumber

        // Build the new state (before status detection)
        let newState =
            {
                Board = newBoard
                ActiveColor = oppositeColor state.ActiveColor
                CastlingRights = newCastling
                EnPassantTarget = newEpTarget
                HalfMoveClock = newHalfMoveClock
                FullMoveNumber = newFullMoveNumber
                History = move :: state.History
                Status = InProgress // Placeholder; updated below
                Version = state.Version + 1
            }

        // Detect game status for the new position
        let status = detectGameStatus newState
        Ok { newState with Status = status }

    /// Convenience: resolve a MoveRequest and apply it in one step.
    let playMove (state: GameState) (request: MoveRequest) : Result<GameState, MoveError> =
        resolveMove state request |> Result.bind (applyMove state)

    /// Create the initial game state (standard starting position).
    let newGame () : GameState =
        match FenParser.parse FenParser.startingFen with
        | Ok state -> state
        | Error msg -> failwith $"Bug: could not parse starting FEN: {msg}"
