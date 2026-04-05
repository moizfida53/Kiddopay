import { HttpClient } from "@angular/common/http";
import { inject, Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { environment } from "src/environments/environment";

@Injectable({ providedIn: 'root' })
export class BaseApiService {
  private http = inject(HttpClient);

  get<T>(url: string, options?: any): Observable<T> {
    return this.http.get<T>(`${environment.apiUrl}${url}`, options) as Observable<T>;
  }

  post<T>(url: string, body: any, options?: any): Observable<T> {
    return this.http.post<T>(`${environment.apiUrl}${url}`, body, options) as Observable<T>;
  }

  delete<T>(url: string, options?: any): Observable<T> {
    return this.http.delete<T>(`${environment.apiUrl}${url}`, options) as Observable<T>;
  }
}
