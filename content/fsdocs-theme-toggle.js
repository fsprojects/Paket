import {LitElement, html, css} from 'https://cdn.jsdelivr.net/gh/lit/dist@3/core/lit-core.min.js';

const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
let currentTheme = localStorage.getItem('theme') ?? (prefersDark ? 'dark' : 'light');

export class ThemeToggle extends LitElement {
    static properties = {
        _theme: {state: true, type: String},
    };

    constructor() {
        super();
        this._theme = currentTheme;
    }

    static styles = css`
      :host {
        cursor: pointer;
        box-sizing: border-box;
        transform: translateY(3px);
      }
    `;

    changeTheme() {
        this._theme = this._theme === 'light' ? 'dark' : 'light';
        localStorage.setItem('theme', this._theme);
        window.document.documentElement.setAttribute("data-theme", this._theme);
    }

    render() {
        const icon = this._theme === 'light' ? 'basil:moon-solid' : 'basil:sun-solid';
        return html`
            <iconify-icon width="30" height="30" icon="${icon}" style="color:var(--header-link-color)" @click=${this.changeTheme}></iconify-icon>
        `;
    }
}

customElements.define('fsdocs-theme-toggle', ThemeToggle);
