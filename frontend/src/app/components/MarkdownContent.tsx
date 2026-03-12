import ReactMarkdown from "react-markdown"
import remarkGfm from "remark-gfm"
import { cn } from "@/lib/utils"

interface MarkdownContentProps {
  content: string
  className?: string
}

export function MarkdownContent({ content, className }: MarkdownContentProps) {
  return (
    <div className={cn("prose prose-sm max-w-none dark:prose-invert", className)}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          table: ({ children, ...props }) => (
            <div className="my-2 overflow-x-auto">
              <table
                className="min-w-full border-collapse text-sm"
                {...props}
              >
                {children}
              </table>
            </div>
          ),
          thead: ({ children, ...props }) => (
            <thead className="border-b bg-muted/50" {...props}>
              {children}
            </thead>
          ),
          th: ({ children, ...props }) => (
            <th
              className="px-3 py-1.5 text-left text-xs font-medium text-muted-foreground"
              {...props}
            >
              {children}
            </th>
          ),
          td: ({ children, ...props }) => (
            <td
              className="border-b px-3 py-1.5 text-sm"
              {...props}
            >
              {children}
            </td>
          ),
          pre: ({ children, ...props }) => (
            <pre
              className="my-2 overflow-x-auto rounded-md bg-muted p-3 text-xs"
              {...props}
            >
              {children}
            </pre>
          ),
          code: ({ children, className: codeClassName, ...props }) => {
            const isInline = !codeClassName
            if (isInline) {
              return (
                <code
                  className="rounded bg-muted px-1 py-0.5 text-xs font-mono"
                  {...props}
                >
                  {children}
                </code>
              )
            }
            return (
              <code className={cn("text-xs", codeClassName)} {...props}>
                {children}
              </code>
            )
          },
          h1: ({ children, ...props }) => (
            <h3 className="mb-2 mt-3 text-base font-semibold" {...props}>
              {children}
            </h3>
          ),
          h2: ({ children, ...props }) => (
            <h4 className="mb-1.5 mt-2.5 text-sm font-semibold" {...props}>
              {children}
            </h4>
          ),
          h3: ({ children, ...props }) => (
            <h5 className="mb-1 mt-2 text-sm font-medium" {...props}>
              {children}
            </h5>
          ),
          p: ({ children, ...props }) => (
            <p className="mb-1.5 last:mb-0" {...props}>
              {children}
            </p>
          ),
          ul: ({ children, ...props }) => (
            <ul className="mb-2 ml-4 list-disc space-y-0.5" {...props}>
              {children}
            </ul>
          ),
          ol: ({ children, ...props }) => (
            <ol className="mb-2 ml-4 list-decimal space-y-0.5" {...props}>
              {children}
            </ol>
          ),
          li: ({ children, ...props }) => (
            <li className="text-sm" {...props}>
              {children}
            </li>
          ),
          blockquote: ({ children, ...props }) => (
            <blockquote
              className="my-2 border-l-2 border-muted-foreground/30 pl-3 italic text-muted-foreground"
              {...props}
            >
              {children}
            </blockquote>
          ),
          hr: (props) => <hr className="my-3 border-muted" {...props} />,
          a: ({ children, ...props }) => (
            <a
              className="text-blue-600 underline underline-offset-2 hover:text-blue-800"
              target="_blank"
              rel="noopener noreferrer"
              {...props}
            >
              {children}
            </a>
          ),
          strong: ({ children, ...props }) => (
            <strong className="font-semibold" {...props}>
              {children}
            </strong>
          ),
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  )
}
