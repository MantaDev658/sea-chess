import { useState, useEffect, useRef } from 'react'
import { Chess } from 'chess.js'
import { Chessboard } from './components/Chessboard'
import { Button } from './components/ui/Button'
import { Card, CardHeader, CardTitle } from './components/ui/Card'
import { Input } from './components/ui/Input'
import {
  Activity,
  Cpu,
  Layers,
  Terminal as TerminalIcon,
  RefreshCw,
  Volume2,
  VolumeX,
  Grid
} from 'lucide-react'

interface MoveRecord {
  num: number
  white: string
  black?: string
  whiteHash: string
  blackHash?: string
}

function App() {
  // Fresh standard game setup
  const [fen, setFen] = useState('rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1')
  const [game, setGame] = useState(() => new Chess())
  const [moves, setMoves] = useState<MoveRecord[]>([])
  const [command, setCommand] = useState('')
  const [commandError, setCommandError] = useState<string | null>(null)
  const [soundEnabled, setSoundEnabled] = useState(true)
  const [latency, setLatency] = useState(12)
  const [hashrate, setHashrate] = useState(2450)
  const ledgerEndRef = useRef<HTMLDivElement>(null)

  // Simulate network tick metrics
  useEffect(() => {
    const timer = setInterval(() => {
      setLatency(prev => {
        const offset = Math.floor(Math.random() * 5) - 2
        return Math.max(8, Math.min(25, prev + offset))
      })
      setHashrate(prev => {
        const offset = Math.floor(Math.random() * 100) - 50
        return Math.max(2200, Math.min(2700, prev + offset))
      })
    }, 3000)
    return () => clearInterval(timer)
  }, [])

  // Auto-scroll the move ledger
  useEffect(() => {
    ledgerEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [moves])

  const generateTxHash = () => {
    const chars = '0123456789abcdef'
    let hash = '0x'
    for (let i = 0; i < 8; i++) {
      hash += chars[Math.floor(Math.random() * 16)]
    }
    return hash
  }

  // Handle board move updates
  const handleBoardMove = (newFen: string) => {
    setFen(newFen)
    
    // Sync current instance
    const localGame = new Chess(newFen)
    setGame(localGame)

    // Reconstruct ledger list
    const history = localGame.history()
    const records: MoveRecord[] = []
    
    for (let i = 0; i < history.length; i += 2) {
      records.push({
        num: Math.floor(i / 2) + 1,
        white: history[i],
        black: history[i + 1] || undefined,
        whiteHash: generateTxHash(),
        blackHash: history[i + 1] ? generateTxHash() : undefined,
      })
    }
    setMoves(records)
    setCommandError(null)
  }

  // Restart match
  const handleRestart = () => {
    const fresh = new Chess()
    setGame(fresh)
    setFen(fresh.fen())
    setMoves([])
    setCommand('')
    setCommandError(null)
  }

  // Command input handler (Algebraic chess terminal commands, e.g. e4, Nf3)
  const handleSendCommand = (e: React.FormEvent) => {
    e.preventDefault()
    if (!command.trim()) return

    const sanitizedCmd = command.trim()

    try {
      // Clone game state to test move
      const tempGame = new Chess(game.fen())
      const result = tempGame.move(sanitizedCmd)
      
      if (result) {
        // Valid move! Apply to actual state
        const nextFen = tempGame.fen()
        setFen(nextFen)
        setGame(tempGame)

        // Update history log
        const history = tempGame.history()
        const records: MoveRecord[] = []
        for (let i = 0; i < history.length; i += 2) {
          records.push({
            num: Math.floor(i / 2) + 1,
            white: history[i],
            black: history[i + 1] || undefined,
            whiteHash: generateTxHash(),
            blackHash: history[i + 1] ? generateTxHash() : undefined,
          })
        }
        setMoves(records)
        setCommand('')
        setCommandError(null)
      }
    } catch {
      setCommandError(`INVALID TX COMMAND: "${sanitizedCmd}" is not a valid algebraic move. Try "e4", "Nf3", "O-O".`)
      // Clear error alert after 4 seconds
      setTimeout(() => setCommandError(null), 4000)
    }
  }

  return (
    <div className="relative min-h-screen bg-void text-pure-light flex flex-col items-center select-none overflow-hidden">
      
      {/* Background Ambience elements */}
      <div className="absolute inset-0 bg-grid-pattern z-0 opacity-40" />
      
      {/* Absolute ambient light blobs */}
      <div className="absolute top-1/4 left-1/4 w-[350px] h-[350px] rounded-full bg-btc-orange/10 blur-[130px] z-0 pointer-events-none" />
      <div className="absolute bottom-1/4 right-1/4 w-[400px] h-[400px] rounded-full bg-gold/5 blur-[150px] z-0 pointer-events-none" />

      {/* Main Glass Header */}
      <header className="relative w-full border-b border-pure-light/10 bg-matter/40 backdrop-blur-md z-10">
        <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between">
          
          {/* Logo Node with Spinning Orbital Rings */}
          <div className="flex items-center gap-3">
            <div className="relative w-9 h-9 flex items-center justify-center">
              <div className="absolute w-full h-full border border-dashed border-btc-orange/40 rounded-full animate-spin-slow" />
              <div className="absolute w-7 h-7 border border-dashed border-gold/40 rounded-full animate-spin-slow-reverse" />
              <div className="w-3.5 h-3.5 bg-gradient-to-r from-burnt-orange to-gold rounded-full shadow-[0_0_12px_rgba(247,147,26,0.6)]" />
            </div>
            <div className="flex flex-col">
              <span className="font-heading font-bold text-md leading-none tracking-wider text-pure-light">
                ECLAIR<span className="bg-gradient-to-r from-btc-orange to-gold bg-clip-text text-transparent ml-1">CORE</span>
              </span>
              <span className="font-mono text-[9px] tracking-widest text-stardust/60 uppercase">
                Chess Platform Terminal
              </span>
            </div>
          </div>

          {/* Glowing Status badge */}
          <div className="flex items-center gap-6">
            <div className="hidden md:flex items-center gap-2 px-3 py-1 border border-pure-light/15 rounded-full bg-matter/60 font-mono text-[10px]">
              <span className="relative flex h-2 w-2">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-gold opacity-75"></span>
                <span className="relative inline-flex rounded-full h-2 w-2 bg-gold"></span>
              </span>
              <span className="text-stardust uppercase">SECURE CONNECT: OK</span>
            </div>

            {/* Audio Toggle */}
            <button
              onClick={() => setSoundEnabled(!soundEnabled)}
              className="p-2 border border-pure-light/10 rounded-full hover:border-btc-orange/50 hover:text-btc-orange transition-all duration-300 cursor-pointer"
              title={soundEnabled ? 'Mute Interface Sounds' : 'Unmute Interface Sounds'}
            >
              {soundEnabled ? <Volume2 size={15} /> : <VolumeX size={15} />}
            </button>
          </div>
        </div>
      </header>

      {/* Main Terminal Body */}
      <main className="relative max-w-7xl w-full mx-auto px-4 py-8 flex-grow z-10 flex flex-col gap-6">
        
        {/* Banner with dramatic Bitcoin DeFi typography */}
        <div className="text-center md:text-left flex flex-col md:flex-row md:items-end justify-between border-b border-pure-light/5 pb-4">
          <div>
            <h1 className="font-heading font-bold text-3xl md:text-5xl tracking-tight leading-tight m-0 text-pure-light">
              Secure Crypto <span className="bg-gradient-to-r from-btc-orange to-gold bg-clip-text text-transparent">Chess Core</span>
            </h1>
            <p className="font-body text-sm md:text-base text-stardust mt-1">
              Zero-latency client predicted move validation. Enter keyboard algebraic commands or select pieces.
            </p>
          </div>
          <div className="flex gap-2 mt-4 md:mt-0 justify-center">
            <Button variant="outline" size="sm" onClick={handleRestart} className="gap-2">
              <RefreshCw size={13} /> Reset Node
            </Button>
          </div>
        </div>

        {/* Dynamic Column Split */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
          
          {/* Chessboard Area (7 Cols) */}
          <div className="lg:col-span-7 flex flex-col items-center">
            <Card className="w-full flex justify-center py-8 px-4 md:px-8 border-pure-light/10 bg-matter/40 backdrop-blur-md holographic-gradient relative overflow-hidden" withCornerAccents hoverEffect={false}>
              
              {/* Subtle visual background grid details */}
              <div className="absolute top-0 right-0 w-32 h-32 border-t border-r border-pure-light/5 pointer-events-none" />
              <div className="absolute bottom-0 left-0 w-32 h-32 border-b border-l border-pure-light/5 pointer-events-none" />

              <Chessboard
                key={fen}
                fen={fen}
                onMove={handleBoardMove}
                disabled={false}
              />
            </Card>

            {/* algebraic Terminal Console Bar (Bottom of Board) */}
            <div className="w-full mt-4">
              <form onSubmit={handleSendCommand} className="flex gap-3">
                <Input
                  value={command}
                  onChange={(e) => setCommand(e.target.value)}
                  placeholder="ENTER ALGEBRAIC MOVE e.g. 'e4', 'Nf3', 'd4'..."
                  withPrefix={<TerminalIcon size={14} />}
                  className="flex-grow"
                />
                <Button type="submit" variant="primary">
                  EXECUTE TX
                </Button>
              </form>
              
              {/* Command Errors warning */}
              {commandError && (
                <div className="mt-2.5 px-3.5 py-2 border border-red-500/20 bg-red-950/20 rounded-lg text-xs font-mono text-red-400 animate-pulse">
                  {commandError}
                </div>
              )}
            </div>
          </div>

          {/* Live Node Statistics & Move Log Sidebar (5 Cols) */}
          <div className="lg:col-span-5 flex flex-col gap-6">
            
            {/* Live Metrics Grid */}
            <div className="grid grid-cols-2 gap-4">
              <Card variant="glass" className="p-4" hoverEffect>
                <div className="flex justify-between items-start">
                  <div className="flex flex-col">
                    <span className="text-[10px] font-mono text-stardust uppercase tracking-wider">Node Latency</span>
                    <span className="font-mono text-lg font-bold text-gold mt-1">{latency} ms</span>
                  </div>
                  <div className="p-1.5 border border-gold/20 bg-gold/10 rounded-lg text-gold">
                    <Activity size={14} />
                  </div>
                </div>
                <div className="text-[9px] font-mono text-stardust/60 mt-2">Zero-latency Client Prediction Active</div>
              </Card>

              <Card variant="glass" className="p-4" hoverEffect>
                <div className="flex justify-between items-start">
                  <div className="flex flex-col">
                    <span className="text-[10px] font-mono text-stardust uppercase tracking-wider">Engine Nodes</span>
                    <span className="font-mono text-lg font-bold text-btc-orange mt-1">{(hashrate / 1000).toFixed(2)} M/s</span>
                  </div>
                  <div className="p-1.5 border border-btc-orange/20 bg-btc-orange/10 rounded-lg text-btc-orange">
                    <Cpu size={14} />
                  </div>
                </div>
                <div className="text-[9px] font-mono text-stardust/60 mt-2">chess.js local thread active</div>
              </Card>
            </div>

            {/* Live Transactions Move Ledger */}
            <Card variant="glass" className="flex-grow flex flex-col h-[350px] lg:h-[400px] p-0 overflow-hidden border-pure-light/10 bg-matter/40 backdrop-blur-md" hoverEffect={false}>
              <CardHeader className="p-4 border-b border-pure-light/5 flex flex-row items-center justify-between">
                <div className="flex items-center gap-2">
                  <Layers size={14} className="text-btc-orange" />
                  <CardTitle className="text-sm font-mono tracking-wider uppercase font-semibold">MOVE TRANSACTION LEDGER</CardTitle>
                </div>
                <span className="font-mono text-[9px] text-stardust/50 px-2 py-0.5 border border-pure-light/5 bg-black/30 rounded-full">
                  BLOCK DEPTH: {moves.length}
                </span>
              </CardHeader>
              
              {/* Scrollable list of moves styled as transaction blocks */}
              <div className="flex-grow overflow-y-auto p-4 space-y-3 font-mono text-xs">
                {moves.length === 0 ? (
                  <div className="h-full flex flex-col items-center justify-center text-stardust/40 space-y-2">
                    <Grid size={24} className="animate-pulse" />
                    <span className="text-[10px] uppercase tracking-wider">No moves recorded in ledger block</span>
                  </div>
                ) : (
                  moves.map((m) => (
                    <div
                      key={m.num}
                      className="group border border-pure-light/5 bg-black/20 rounded-xl p-3 flex flex-col gap-2 hover:border-btc-orange/30 transition-all duration-300"
                    >
                      {/* Move sequence tag */}
                      <div className="flex justify-between items-center text-[10px] text-stardust/50 border-b border-pure-light/5 pb-1">
                        <span>BLOCK #{m.num.toString().padStart(3, '0')}</span>
                        <span className="text-gold font-semibold uppercase">PROVEN VALID</span>
                      </div>

                      {/* Side by side player move transactions */}
                      <div className="grid grid-cols-2 gap-4">
                        {/* White transaction */}
                        <div className="flex flex-col gap-0.5">
                          <span className="text-[9px] text-stardust/40 uppercase">GOLD MOVE TX</span>
                          <div className="flex items-center justify-between">
                            <span className="text-gold font-bold text-sm">{m.white}</span>
                            <span className="text-[8px] text-stardust/60 font-mono">{m.whiteHash}</span>
                          </div>
                        </div>

                        {/* Black transaction */}
                        {m.black ? (
                          <div className="flex flex-col gap-0.5 border-l border-pure-light/5 pl-4">
                            <span className="text-[9px] text-stardust/40 uppercase">ORANGE MOVE TX</span>
                            <div className="flex items-center justify-between">
                              <span className="text-btc-orange font-bold text-sm">{m.black}</span>
                              <span className="text-[8px] text-stardust/60 font-mono">{m.blackHash}</span>
                            </div>
                          </div>
                        ) : (
                          <div className="flex items-center pl-4 text-[9px] text-stardust/30 animate-pulse uppercase tracking-wider">
                            Pending Tx...
                          </div>
                        )}
                      </div>
                    </div>
                  ))
                )}
                <div ref={ledgerEndRef} />
              </div>
            </Card>
          </div>
        </div>
      </main>

      {/* Cyber Technical Ticker Footer */}
      <footer className="w-full border-t border-pure-light/10 bg-matter/20 mt-12 py-4 text-center font-mono text-[9px] text-stardust/40 tracking-wider">
        <div className="max-w-7xl mx-auto px-6 flex flex-col sm:flex-row items-center justify-between gap-2">
          <span>ECLAIR SYSTEM NODE v1.0.0-PROTOTYPE</span>
          <span className="flex items-center gap-1.5">
            <span className="w-1.5 h-1.5 rounded-full bg-gold animate-ping" />
            LIVE TELEMETRY BROADCAST ACTIVE
          </span>
        </div>
      </footer>
    </div>
  )
}

export default App
