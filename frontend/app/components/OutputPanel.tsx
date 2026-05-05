"use client";

import { useEffect, useRef } from "react";

interface Props {
  lines: string[];
  executionTime?: string;
  isRunning: boolean;
}

export default function OutputPanel({ lines, executionTime, isRunning }: Props) {
  const bottomRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom as new lines arrive
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [lines]);

  return (
    <div className="flex flex-col h-full bg-[#1e1e1e] rounded-lg overflow-hidden border border-[#333]">
      <div className="flex items-center justify-between px-4 py-2 bg-[#252526] border-b border-[#333]">
        <span className="text-xs text-[#858585] uppercase tracking-widest">Output</span>
        {executionTime && (
          <span className="text-xs text-[#4ec9b0]">{executionTime}</span>
        )}
        {isRunning && (
          <span className="flex items-center gap-1.5 text-xs text-[#ce9178]">
            <span className="w-1.5 h-1.5 bg-[#ce9178] rounded-full animate-pulse" />
            Running
          </span>
        )}
      </div>
      <div className="flex-1 overflow-y-auto p-4 font-mono text-sm">
        {lines.length === 0 ? (
          <p className="text-[#555]">Output will appear here...</p>
        ) : (
          lines.map((line, i) => (
            <div
              key={i}
              className={`leading-6 whitespace-pre-wrap ${
                line.toLowerCase().includes("error")
                  ? "text-[#f48771]"
                  : line.startsWith("Preparing") || line.startsWith("Running")
                  ? "text-[#858585] italic"
                  : "text-[#d4d4d4]"
              }`}
            >
              {line}
            </div>
          ))
        )}
        <div ref={bottomRef} />
      </div>
    </div>
  );
}
