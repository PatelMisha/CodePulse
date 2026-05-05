"use client";

import { useEffect, useRef } from "react";

interface Props {
  review: string;
  isReviewing: boolean;
}

export default function AIReviewPanel({ review, isReviewing }: Props) {
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [review]);

  return (
    <div className="flex flex-col h-full bg-[#1e1e1e] rounded-lg overflow-hidden border border-[#333]">
      <div className="flex items-center justify-between px-4 py-2 bg-[#252526] border-b border-[#333]">
        <div className="flex items-center gap-2">
          <span className="text-xs text-[#858585] uppercase tracking-widest">AI Review</span>
          <span className="text-xs text-[#569cd6] bg-[#1a1a2e] px-2 py-0.5 rounded">Claude</span>
        </div>
        {isReviewing && (
          <span className="flex items-center gap-1.5 text-xs text-[#569cd6]">
            <span className="w-1.5 h-1.5 bg-[#569cd6] rounded-full animate-pulse" />
            Reviewing
          </span>
        )}
      </div>
      <div className="flex-1 overflow-y-auto p-4 text-sm leading-7 text-[#d4d4d4] whitespace-pre-wrap">
        {!review && !isReviewing && (
          <p className="text-[#555]">AI feedback will appear here after your code runs...</p>
        )}
        {review}
        {isReviewing && (
          <span className="inline-block w-2 h-4 bg-[#569cd6] ml-0.5 animate-pulse align-middle" />
        )}
        <div ref={bottomRef} />
      </div>
    </div>
  );
}
