namespace ChessPlatform.Domain

// ─────────────────────────────────────────────────────────
//  Types.fs — Pure domain types. Zero dependencies.
// ─────────────────────────────────────────────────────────

/// The two sides of the board.
type Color =
    | White
    | Black

/// Standard chess piece kinds.
type PieceType =
    | King
    | Queen
    | Rook
    | Bishop
    | Knight
    | Pawn

/// A piece on the board: a color and a kind.
type Piece = { Color: Color; Type: PieceType }

/// A square on the board, represented as (file, rank) where both are 0-7.
/// File 0 = a, File 7 = h.  Rank 0 = 1, Rank 7 = 8.
type Position = int * int

/// The board is a map from occupied positions to the piece on that square.
/// Absent keys mean empty squares.
type Board = Map<Position, Piece>

/// Which side to castle toward.
type CastlingSide =
    | KingSide
    | QueenSide

/// A fully-described chess move, carrying all data needed
/// to apply it to the board without ambiguity.
type Move =
    | Normal of From: Position * To: Position
    | Capture of From: Position * To: Position * Captured: Piece
    | Castle of Color: Color * Side: CastlingSide
    | EnPassant of From: Position * To: Position * CapturedPawnPos: Position
    | Promotion of From: Position * To: Position * PromoteTo: PieceType
    | PromotionCapture of From: Position * To: Position * Captured: Piece * PromoteTo: PieceType

/// Tracks which castling moves are still available.
type CastlingRights = {
    WhiteKingSide: bool
    WhiteQueenSide: bool
    BlackKingSide: bool
    BlackQueenSide: bool
}

/// The current status of the game.
type GameStatus =
    | InProgress
    | Check of Color
    | Checkmate of Winner: Color
    | Stalemate
    | Draw of Reason: string

/// Complete, immutable snapshot of a chess game.
type GameState = {
    Board: Board
    ActiveColor: Color
    CastlingRights: CastlingRights
    EnPassantTarget: Position option
    HalfMoveClock: int
    FullMoveNumber: int
    History: Move list
    Status: GameStatus
    Version: int
}

/// Reasons a move can be rejected.
type MoveError =
    | NoPieceAtSource
    | WrongColor
    | InvalidMove of string
    | MoveLeavesKingInCheck
    | GameAlreadyOver

/// A raw move request from the outside world (UI / API).
/// The domain logic resolves this into a fully-typed Move.
type MoveRequest = {
    From: Position
    To: Position
    Promotion: PieceType option
}
