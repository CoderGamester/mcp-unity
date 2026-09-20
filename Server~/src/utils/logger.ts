import { appendFile } from 'fs/promises';

export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3
}

// Check environment variable for logging
const isLoggingEnabled = process.env.LOGGING === 'true';

// Check environment variable for logging in a file
const isLoggingFileEnabled = process.env.LOGGING_FILE === 'true';

// Async log queue to avoid blocking the main thread
const logQueue: Array<() => Promise<void>> = [];
let isProcessingQueue = false;

async function processLogQueue(): Promise<void> {
  if (isProcessingQueue || logQueue.length === 0) return;
  
  isProcessingQueue = true;
  
  while (logQueue.length > 0) {
    const logTask = logQueue.shift();
    if (logTask) {
      try {
        await logTask();
      } catch (error) {
        console.error('Failed to write to log file:', error);
      }
    }
  }
  
  isProcessingQueue = false;
}

function enqueueLogWrite(logTask: () => Promise<void>): void {
  logQueue.push(logTask);
  // Process queue asynchronously without blocking
  setImmediate(() => processLogQueue());
}

export class Logger {
  private level: LogLevel;
  private prefix: string;
  
  constructor(prefix: string, level: LogLevel = LogLevel.INFO) {
    this.prefix = prefix;
    this.level = level;
  }
  
  debug(message: string, data?: any) {
    this.log(LogLevel.DEBUG, message, data);
  }
  
  info(message: string, data?: any) {
    this.log(LogLevel.INFO, message, data);
  }
  
  warn(message: string, data?: any) {
    this.log(LogLevel.WARN, message, data);
  }
  
  error(message: string, error?: any) {
    this.log(LogLevel.ERROR, message, error);
  }
  
  isLoggingEnabled(): boolean {
    return isLoggingEnabled;
  }
  
  isLoggingFileEnabled(): boolean {
    return isLoggingFileEnabled;
  }
  
  private log(level: LogLevel, message: string, data?: any) {
    if (level < this.level) return;
    
    const timestamp = new Date().toISOString();
    const levelStr = LogLevel[level];
    const logMessage = `[${timestamp}] [${levelStr}] [${this.prefix}] ${message}`;

    // Write to file if file logging is enabled (async, non-blocking)
    if (this.isLoggingFileEnabled()) {
      enqueueLogWrite(async () => {
        await appendFile('log.txt', logMessage + '\n');
        if (data) {
          await appendFile('log.txt', JSON.stringify(data, null, 2) + '\n');
        }
      });
    }
    
    // Write to console if logging is enabled
    if (this.isLoggingEnabled()) {
      if (data) {
        console.log(logMessage, data);
      } else {
        console.log(logMessage);
      }
    }
  }
}