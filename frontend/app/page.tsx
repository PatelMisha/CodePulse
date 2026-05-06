"use client";

import { useState } from "react";
import * as signalR from "@microsoft/signalr";
import axios from "axios";
import CodeEditor from "./components/CodeEditor";
import OutputPanel from "./components/OutputPanel";
import AIReviewPanel from "./components/AIReviewPanel";

const API = "http://localhost:5167";

export default function Home() {
  const [language, setLanguage] = useState("python");
  const [code, setCode] = useState("");
  const [isRunning, setIsRunning] = useState(false);
  const [isReviewing, setIsReviewing] = useState(false);
  const [outputLines, setOutputLines] = useState<string[]>([]);
  const [aiReview, setAiReview] = useState("");
  const [executionTime, setExecutionTime] = useState<string>();
  const [error, setError] = useState<string>();

  const handleRun = async () => {
    if (!code.trim() || isRunning) return;

    // Reset state
    setIsRunning(true);
    setIsReviewing(false);
    setOutputLines([]);
    setAiReview("");
    setExecutionTime(undefined);
    setError(undefined);

    try {
      // 1. Submit code to backend — get back a submissionId
      const { data } = await axios.post(`${API}/api/submission`, { code, language });
      const submissionId = data.submissionId;

      // 2. Connect to SignalR hub and join the submission's group
      // All output and AI feedback for this run streams through this connection
      const connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API}/hubs/execution`)
        .withAutomaticReconnect()
        .build();

      connection.on("outputLine", (line: string) => {
        setOutputLines((prev) => [...prev, line]);
      });

      connection.on("executionComplete", (summary: string) => {
        setIsRunning(false);
        setExecutionTime(summary.trim());
      });

      connection.on("reviewStarted", () => {
        setIsReviewing(true);
      });

      connection.on("reviewChunk", (chunk: string) => {
        setAiReview((prev) => prev + chunk);
      });

      connection.on("reviewComplete", () => {
        setIsReviewing(false);
        connection.stop();
      });

      // Safety timeout — reset UI if nothing completes within 90s
      const timeout = setTimeout(() => {
        setIsRunning(false);
        setIsReviewing(false);
        setOutputLines((prev) => [...prev, "\nConnection timed out. Please try again."]);
        connection.stop();
      }, 90000);

      await connection.start();
      await connection.invoke("JoinSubmission", submissionId);
    } catch (err: unknown) {
      setIsRunning(false);
      if (axios.isAxiosError(err) && err.response?.status === 429) {
        setError("Rate limit exceeded. Max 10 runs per minute.");
      } else {
        setError("Failed to connect. Make sure the backend is running.");
      }
    }
  };

  return (
    <div className="flex flex-col h-screen bg-[#0d0d0d] text-white overflow-hidden">
      {/* Header */}
      <header className="flex items-center justify-between px-6 py-3 bg-[#1a1a1a] border-b border-[#333] shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-7 h-7 bg-[#00f0ff] rounded flex items-center justify-center">
            <span className="text-black text-xs font-black">CP</span>
          </div>
          <span className="font-semibold text-white tracking-tight">CodePulse</span>
          <span className="text-xs text-[#555] hidden sm:block">
            AI-powered code execution & review
          </span>
        </div>
        <a
          href="https://github.com/PatelMisha/CodePulse"
          target="_blank"
          rel="noreferrer"
          className="text-xs text-[#555] hover:text-[#aaa] transition-colors"
        >
          GitHub
        </a>
      </header>

      {/* Error banner */}
      {error && (
        <div className="mx-4 mt-3 px-4 py-2 bg-[#3a1a1a] border border-[#f48771]/40 rounded text-[#f48771] text-sm shrink-0">
          {error}
        </div>
      )}

      {/* Main layout — editor left, output + review right */}
      <main className="flex flex-1 gap-3 p-3 overflow-hidden">
        {/* Left — code editor */}
        <div className="flex-1 min-w-0">
          <CodeEditor
            code={code}
            language={language}
            onCodeChange={setCode}
            onLanguageChange={setLanguage}
            onRun={handleRun}
            isRunning={isRunning}
          />
        </div>

        {/* Right — output + AI review stacked */}
        <div className="w-[420px] flex flex-col gap-3 shrink-0">
          <div className="flex-[0.4] min-h-0">
            <OutputPanel
              lines={outputLines}
              executionTime={executionTime}
              isRunning={isRunning}
            />
          </div>
          <div className="flex-[0.6] min-h-0">
            <AIReviewPanel review={aiReview} isReviewing={isReviewing} />
          </div>
        </div>
      </main>
    </div>
  );
}
