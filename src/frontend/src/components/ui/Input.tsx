import * as React from 'react'
import { cn } from '../../lib/utils'

export interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  withPrefix?: React.ReactNode
}

const Input = React.forwardRef<HTMLInputElement, InputProps>(
  ({ className, type = 'text', withPrefix, ...props }, ref) => {
    return (
      <div className="relative flex items-center w-full">
        {withPrefix && (
          <div className="absolute left-3 text-btc-orange font-mono text-sm pointer-events-none select-none">
            {withPrefix}
          </div>
        )}
        <input
          type={type}
          className={cn(
            'flex h-12 w-full bg-black/40 border-b-2 border-pure-light/20 px-3 py-2 text-sm text-pure-light font-mono placeholder:text-pure-light/30 transition-all duration-300 focus:border-btc-orange focus:shadow-[0_10px_20px_-10px_rgba(247,147,26,0.35)] focus:outline-none disabled:cursor-not-allowed disabled:opacity-50',
            withPrefix && 'pl-8',
            className
          )}
          ref={ref}
          {...props}
        />
      </div>
    )
  }
)
Input.displayName = 'Input'

export { Input }
