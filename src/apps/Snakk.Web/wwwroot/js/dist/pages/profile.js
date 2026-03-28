"use strict";(function(){"use strict";const l=window.SnakkUtils?.escapeHtml||function(r){if(!r)return"";const c=document.createElement("div");return c.textContent=r,c.innerHTML},S=window.SnakkUtils?.sanitizeHtml||function(r){if(!r)return"";const g=new DOMParser().parseFromString(r,"text/html");return g.querySelectorAll("script,iframe,object,embed,form,base,meta,link,style").forEach(a=>a.remove()),g.body.querySelectorAll("*").forEach(a=>{Array.from(a.attributes).forEach(u=>{u.name.startsWith("on")&&a.removeAttribute(u.name)}),["href","src","action","formaction"].forEach(u=>{const L=a.getAttribute(u);L&&L.trim().toLowerCase().startsWith("javascript:")&&a.removeAttribute(u)})}),g.body.innerHTML},x=window.SnakkUtils?.sanitizeUrl||function(r){if(!r)return"#";const c=r.trim().toLowerCase();return c.startsWith("javascript:")||c.startsWith("data:")?"#":r},M=window.SnakkUtils?.formatRelativeTime||function(r){const c=new Date(r),a=Math.floor((new Date().getTime()-c.getTime())/1e3);if(a<60)return"just now";if(a<3600)return`${Math.floor(a/60)}m ago`;if(a<86400)return`${Math.floor(a/3600)}h ago`;if(a<604800)return`${Math.floor(a/86400)}d ago`;const u=window.snakkTimezone||"UTC";try{return c.toLocaleDateString("en-US",{timeZone:u,month:"short",day:"numeric",year:"numeric"})}catch{return c.toLocaleDateString("en-US",{month:"short",day:"numeric",year:"numeric"})}},j=document.getElementById("profile-page-config");if(!j)return;const b=JSON.parse(j.textContent||"{}"),w=b.userId,$={totalActivity:b.totalActivity,daysSinceJoined:b.daysSinceJoined,discussionCount:b.discussionCount,postCount:b.postCount};function T(){async function r(){try{const t=await(await fetch(`/bff/users/${w}/stats`)).json(),e=document.getElementById("stat-followers");e&&(e.textContent=t.followerCount||0);const n=document.getElementById("stat-replies");n&&t.replyCount!==void 0&&(n.textContent=t.replyCount)}catch(o){console.error("Error loading user stats:",o);const t=document.getElementById("stat-followers");t&&(t.textContent="0")}}async function c(o){const t=document.getElementById("recent-discussions");if(t)try{const n=await(await fetch(`/bff/search/discussions?authorPublicId=${w}&pageSize=${o}`)).json();if(!n.items||n.items.length===0){t.innerHTML=`
                        <div class="text-center py-8 text-muted">
                            <p>No discussions yet</p>
                        </div>
                    `;return}t.innerHTML=`<div class="topic-list">${n.items.map(s=>`
                    <div class="topic-item-wrapper">
                        <div class="topic-item">
                            <div class="topic-content">
                                <div class="topic-title">
                                    <a href="${x(s.url)}" class="topic-title-link">${l(s.title)}</a>
                                </div>
                                <div class="topic-meta">
                                    <span class="topic-meta-link">${l(s.hubName)}</span>
                                    <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"></polyline></svg>
                                    <span class="topic-meta-link">${l(s.spaceName)}</span>
                                    <span class="topic-meta-separator">&middot;</span>
                                    <span>${M(s.lastActivityAt||s.createdAt)}</span>
                                </div>
                            </div>
                            <div class="topic-stats hidden sm:flex">
                                <div class="topic-stat">
                                    <div class="topic-stat-icon">
                                        <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3zM7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"></path>
                                        </svg>
                                    </div>
                                    <div class="topic-stat-value">${s.reactionCount}</div>
                                </div>
                                <div class="topic-stat">
                                    <div class="topic-stat-icon">
                                        <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
                                        </svg>
                                    </div>
                                    <div class="topic-stat-value">${s.postCount}</div>
                                </div>
                            </div>
                            <a href="${x(s.url)}" class="topic-latest-link" title="Go to discussion">
                                <svg class="chevron-right" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="6 9 12 15 18 9"></polyline>
                                </svg>
                            </a>
                        </div>
                    </div>
                `).join("")}</div>`}catch(e){console.error("Error loading discussions:",e),t.innerHTML='<div class="text-center py-8 text-error">Failed to load discussions</div>'}}async function g(o){const t=document.getElementById("recent-posts");if(t)try{const n=await(await fetch(`/bff/search/posts?authorPublicId=${w}&pageSize=${o}`)).json();if(!n.items||n.items.length===0){t.innerHTML=`
                        <div class="text-center py-8 text-muted">
                            <p>No posts yet</p>
                        </div>
                    `;return}t.innerHTML=`<div class="topic-list">${n.items.map(s=>`
                    <div class="topic-item-wrapper">
                        <div class="topic-item">
                            <div class="topic-content">
                                <div class="topic-title">
                                    <a href="${x(s.url)}" class="topic-title-link">${l(s.discussionTitle)}</a>
                                </div>
                                <div class="topic-meta">
                                    <span class="topic-meta-link">${l(s.hubName)}</span>
                                    <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"></polyline></svg>
                                    <span class="topic-meta-link">${l(s.spaceName)}</span>
                                    <span class="topic-meta-separator">&middot;</span>
                                    <span>${M(s.createdAt)}</span>
                                </div>
                                <div class="prose prose-sm max-w-none mt-1 text-sm text-base-content/70 line-clamp-2">
                                    ${S(s.contentPreview)}
                                </div>
                            </div>
                            <a href="${x(s.url)}" class="topic-latest-link" title="Go to discussion">
                                <svg class="chevron-right" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="6 9 12 15 18 9"></polyline>
                                </svg>
                            </a>
                        </div>
                    </div>
                `).join("")}</div>`}catch(e){console.error("Error loading posts:",e),t.innerHTML='<div class="text-center py-8 text-error">Failed to load posts</div>'}}async function a(o){const t=document.getElementById("activity-chart-sidebar"),e=document.getElementById("activity-chart-main"),s=window.matchMedia("(min-width: 1024px)").matches?t:e;if(s)try{const h=((await(await fetch(`/bff/users/${w}/activity-history?days=${o}`)).json()).activities||[]).map(m=>({date:m.date,discussions:m.discussionCount??0,posts:m.postCount??0,total:(m.discussionCount??0)+(m.postCount??0)}));u(s,h,o)}catch(p){console.error("Error loading activity chart:",p),s.innerHTML='<div class="text-center py-8 text-error">Failed to load activity chart</div>'}}function u(o,t,e){if(!t||t.length===0){o.innerHTML=`
                    <div class="text-center py-12">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                        </svg>
                        <h3 class="font-semibold mb-2">No activity yet</h3>
                        <p class="text-sm text-muted">Activity will appear here once this user starts contributing</p>
                    </div>
                `;return}const n=Math.max(...t.map(i=>i.total),1),s=150,p=e>30;let y=t;if(p){const i=[];for(let d=0;d<t.length;d+=7){const v=t.slice(d,d+7);if(v.length===0||!v[0])continue;const C={date:v[0].date,discussions:v.reduce((k,f)=>k+f.discussions,0),posts:v.reduce((k,f)=>k+f.posts,0),total:v.reduce((k,f)=>k+f.total,0),isWeek:!0};i.push(C)}y=i}const h=y.map(i=>{const d=n>0?i.total/n*100:0,v=i.total>0?i.discussions/i.total*100:0,C=i.total>0?i.posts/i.total*100:0,f={timeZone:window.snakkTimezone||"UTC",month:"short",day:"numeric"},H=E=>{try{return new Date(E).toLocaleDateString("en-US",f)}catch{return new Date(E).toLocaleDateString("en-US",{month:"short",day:"numeric"})}},F=p?`Week of ${H(i.date)}`:H(i.date);return`
                    <div class="activity-chart-bar-wrapper">
                        <div class="activity-chart-bar-container" style="height: ${s}px;">
                            <div class="activity-chart-bar"
                                 style="height: ${i.total===0?"4px":d+"%"}; ${i.total===0?"min-height: 4px;":""}"
                                 title="${i.total} contribution${i.total!==1?"s":""}\\n${i.discussions} discussion${i.discussions!==1?"s":""}\\n${i.posts} post${i.posts!==1?"s":""}\\n${F}">
                                ${i.discussions>0?`<div class="activity-chart-bar-segment-primary" style="height: ${v}%;"></div>`:""}
                                ${i.posts>0?`<div class="activity-chart-bar-segment-secondary" style="height: ${C}%;"></div>`:""}
                                ${i.total===0?'<div class="activity-chart-bar-zero"></div>':""}
                            </div>
                        </div>
                    </div>
                `}).join(""),m=t.reduce((i,d)=>i+d.discussions,0),A=t.reduce((i,d)=>i+d.posts,0),D=m+A;o.innerHTML=`
                <div class="space-y-4">
                    <div class="activity-chart-wrapper" style="height: ${s+40}px;">
                        ${h}
                    </div>
                    <div class="activity-chart-legend">
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-primary"></div>
                            <span>${m} discussions</span>
                        </div>
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-secondary"></div>
                            <span>${A} posts</span>
                        </div>
                        <span class="text-base-content/50">(${D} total)</span>
                    </div>
                </div>
            `}async function L(){const o=document.getElementById("name-history-section"),t=document.getElementById("name-history-list");if(!(!o||!t))try{const n=await(await fetch("/bff/me/display-name-history",{credentials:"include"})).json();if(!n.entries||n.entries.length===0)return;t.innerHTML=n.entries.map(s=>`
                    <div class="flex items-center justify-between text-sm py-1.5">
                        <div class="min-w-0">
                            <span class="text-base-content/50 line-through">${l(s.previousName)}</span>
                            <span class="text-base-content/40 mx-1">&rarr;</span>
                            <span class="font-medium">${l(s.newName)}</span>
                        </div>
                        <span class="text-xs text-base-content/40 shrink-0 ml-2">${M(s.changedAt)}</span>
                    </div>
                `).join(""),o.classList.remove("hidden")}catch(e){console.error("Error loading name history:",e)}}async function z(){const o=document.getElementById("profile-actions");if(o)try{const e=await(await fetch("/bff/auth/status",{credentials:"include"})).json();if(!e.isAuthenticated){o.innerHTML="";return}if(e.publicId===w){o.innerHTML=`
                        <a href="/settings" class="btn btn-outline btn-sm">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                            </svg>
                            Edit Profile
                        </a>
                    `,L();return}const s=await(await fetch(`/bff/users/${w}/follow-status?currentUserId=${e.publicId}`,{credentials:"include"})).json();o.innerHTML=`
                    <button data-action="toggle-follow-user"
                            data-user-id="${w}"
                            class="btn ${s.isFollowing?"btn-outline":"btn-primary"} btn-sm"
                            id="follow-btn">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="${s.isFollowing?"M5 13l4 4L19 7":"M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"}" />
                        </svg>
                        <span id="follow-btn-text">${s.isFollowing?"Following":"Follow"}</span>
                    </button>
                `}catch(t){console.error("Error loading profile actions:",t),o.innerHTML=""}}async function B(o){const t=document.getElementById("follow-btn"),e=document.getElementById("follow-btn-text");if(!(!t||!e)){t.disabled=!0;try{const n=await fetch(`/bff/users/${o}/follow`,{method:"POST",credentials:"include"});if(n.ok){const s=await n.json();e.textContent=s.isFollowing?"Following":"Follow",s.isFollowing?(t.classList.remove("btn-primary"),t.classList.add("btn-outline")):(t.classList.remove("btn-outline"),t.classList.add("btn-primary")),r()}else throw new Error("Failed to toggle follow")}catch(n){console.error("Error toggling follow:",n),alert("Failed to update follow status")}finally{t.disabled=!1}}}function I(){const o=document.getElementById("achievements-section"),t=document.getElementById("achievements-grid");if(!o||!t)return;const e=[],n=$.totalActivity,s=$.daysSinceJoined,p=$.discussionCount,y=$.postCount;n>=1e3?e.push({name:"Power User",icon:'<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M16.5 18.75h-9m9 0a3 3 0 013 3h-15a3 3 0 013-3m9 0v-4.5A3.375 3.375 0 0012.75 10.875h-.75a3.375 3.375 0 00-3.375 3.375v4.5m9-9L12 3l-4.125 6.75" />',color:"text-warning",description:"1000+ contributions"}):n>=500?e.push({name:"Super Contributor",icon:'<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M11.48 3.499a.562.562 0 011.04 0l2.125 5.111a.563.563 0 00.475.345l5.518.442c.499.04.701.663.321.988l-4.204 3.602a.563.563 0 00-.182.557l1.285 5.385a.562.562 0 01-.84.61l-4.725-2.885a.563.563 0 00-.586 0L6.982 20.54a.562.562 0 01-.84-.61l1.285-5.386a.562.562 0 00-.182-.557l-4.204-3.602a.563.563 0 01.321-.988l5.518-.442a.563.563 0 00.475-.345L11.48 3.5z" />',color:"text-info",description:"500+ contributions"}):n>=100&&e.push({name:"Active Member",icon:'<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9.813 15.904L9 18.75l-.813-2.846a4.5 4.5 0 00-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 003.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 003.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 00-3.09 3.09zM18.259 8.715L18 9.75l-.259-1.035a3.375 3.375 0 00-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 002.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 002.455 2.456L21.75 6l-1.036.259a3.375 3.375 0 00-2.455 2.456zM16.894 20.567L16.5 21.75l-.394-1.183a2.25 2.25 0 00-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 001.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 001.423 1.423l1.183.394-1.183.394a2.25 2.25 0 00-1.423 1.423z" />',color:"text-success",description:"100+ contributions"}),p>=50&&e.push({name:"Discussion Starter",icon:'<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20.25 8.511c.884.284 1.5 1.128 1.5 2.097v4.286c0 1.136-.847 2.1-1.98 2.193-.34.027-.68.052-1.02.072v3.091l-3-3c-1.354 0-2.694-.055-4.02-.163a2.115 2.115 0 01-.825-.242m9.345-8.334a2.126 2.126 0 00-.476-.095 48.64 48.64 0 00-8.048 0c-1.131.094-1.976 1.057-1.976 2.192v4.286c0 .837.46 1.58 1.155 1.951m9.345-8.334V6.637c0-1.621-1.152-3.026-2.76-3.235A48.455 48.455 0 0011.25 3c-2.115 0-4.198.137-6.24.402-1.608.209-2.76 1.614-2.76 3.235v6.226c0 1.621 1.152 3.026 2.76 3.235.577.075 1.157.14 1.74.194V21l4.155-4.155" />',color:"text-primary",description:"50+ discussions"}),y>=100&&y>p*3&&e.push({name:"Conversationalist",icon:'<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.129.166 2.27.293 3.423.379.35.026.67.21.865.501L12 21l2.755-4.133a1.14 1.14 0 01.865-.501 48.172 48.172 0 003.423-.379c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z" />',color:"text-accent",description:"Highly engaged in discussions"}),s>=365?e.push({name:"Veteran",icon:'<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />',color:"text-secondary",description:"Member for over a year"}):s>=180&&e.push({name:"Regular",icon:'<path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M6.75 3v2.25M17.25 3v2.25M3 18.75V7.5a2.25 2.25 0 012.25-2.25h13.5A2.25 2.25 0 0121 7.5v11.25m-18 0A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75m-18 0v-7.5A2.25 2.25 0 015.25 9h13.5A2.25 2.25 0 0121 11.25v7.5" />',color:"text-neutral-content",description:"Member for 6+ months"}),e.length!==0&&(t.innerHTML=e.map(h=>`
                <div class="achievement-item" title="${l(h.description)}">
                    <div class="achievement-icon ${h.color}">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            ${h.icon}
                        </svg>
                    </div>
                    <span class="achievement-name">${l(h.name)}</span>
                </div>
            `).join(""),o.classList.remove("hidden"))}r(),z(),I(),a(30),c(5),g(5),document.addEventListener("click",async o=>{const t=o.target;if(!t)return;const e=t.closest("[data-action]");if(!e||!e.dataset.action)return;switch(e.dataset.action){case"toggle-follow-user":o.preventDefault(),e.dataset.userId&&await B(e.dataset.userId);break;case"load-activity-chart":o.preventDefault(),e.dataset.days&&await a(parseInt(e.dataset.days,10));break}})}T()})();
