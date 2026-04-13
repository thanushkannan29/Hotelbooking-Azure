import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ChatMessage {
  role: 'user' | 'model';
  text: string;
}

@Injectable({ providedIn: 'root' })
export class ChatbotService {
  private http = inject(HttpClient);
  private readonly chatUrl = `${environment.apiUrl}/chatbot/chat`;

  send(history: ChatMessage[], userMessage: string, systemPrompt: string): Observable<string> {
    // Keep last 6 messages only to stay within token limits
    const recentHistory = history.slice(-6);

    const body = {
      userMessage,
      systemPrompt,
      history: recentHistory.map(m => ({ role: m.role, text: m.text }))
    };

    return this.http.post<{ reply: string }>(this.chatUrl, body).pipe(
      map(res => res.reply ?? 'Sorry, I could not get a response. Please try again.')
    );
  }
}
