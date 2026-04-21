"use strict";(function(){"use strict";const f=window.SnakkUtils?.escapeHtml||function(a){if(!a)return"";const c=document.createElement("div");return c.textContent=a,c.innerHTML},E=window.DOMPurify,I=a=>a&&E?E.sanitize(a,{USE_PROFILES:{html:!0}}):"",C=window.SnakkUtils?.sanitizeUrl||function(a){if(!a)return"#";const c=a.trim().toLowerCase();return c.startsWith("javascript:")||c.startsWith("data:")?"#":a},L=window.SnakkUtils?.formatRelativeTime||function(a){const c=new Date(a),r=Math.floor((new Date().getTime()-c.getTime())/1e3);if(r<60)return"just now";if(r<3600)return`${Math.floor(r/60)}m ago`;if(r<86400)return`${Math.floor(r/3600)}h ago`;if(r<604800)return`${Math.floor(r/86400)}d ago`;const b=window.snakkTimezone||"UTC";try{return c.toLocaleDateString("en-US",{timeZone:b,month:"short",day:"numeric",year:"numeric"})}catch{return c.toLocaleDateString("en-US",{month:"short",day:"numeric",year:"numeric"})}},M=document.getElementById("profile-page-config");if(!M)return;const h=JSON.parse(M.textContent||"{}").userId;function S(){async function a(){try{const t=await(await fetch(`/bff/users/${h}/stats`)).json(),s=document.getElementById("stat-followers");s&&(s.textContent=t.followerCount||0);const i=document.getElementById("stat-replies");i&&t.replyCount!==void 0&&(i.textContent=t.replyCount)}catch(n){console.error("Error loading user stats:",n);const t=document.getElementById("stat-followers");t&&(t.textContent="0")}}async function c(n){const t=document.getElementById("recent-posts");if(t)try{const i=await(await fetch(`/bff/search/posts?authorPublicId=${h}&pageSize=${n}`)).json();if(!i.items||i.items.length===0){t.innerHTML=`
                        <div class="text-center py-8 text-muted">
                            <p>No posts yet</p>
                        </div>
                    `;return}t.innerHTML=`<div class="topic-list">${i.items.map(o=>`
                    <div class="topic-item-wrapper">
                        <div class="topic-item">
                            <div class="topic-content">
                                <div class="topic-title">
                                    <a href="${C(o.url)}" class="topic-title-link">${f(o.discussionTitle)}</a>
                                </div>
                                <div class="topic-meta">
                                    <span class="topic-meta-link">${f(o.hubName)}</span>
                                    <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"></polyline></svg>
                                    <span class="topic-meta-link">${f(o.spaceName)}</span>
                                    <span class="topic-meta-separator">&middot;</span>
                                    <span>${L(o.createdAt)}</span>
                                </div>
                                <div class="prose prose-sm max-w-none mt-1 text-sm text-base-content/70 fp-post-preview">
                                    ${I(o.contentPreview)}
                                </div>
                            </div>
                            <a href="${C(o.url)}" class="topic-latest-link" title="Go to discussion">
                                <svg class="chevron-right" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="6 9 12 15 18 9"></polyline>
                                </svg>
                            </a>
                        </div>
                    </div>
                `).join("")}</div>`}catch(s){console.error("Error loading posts:",s),t.innerHTML='<div class="text-center py-8 text-error">Failed to load posts</div>'}}async function v(n){const t=document.getElementById("activity-chart-sidebar"),s=document.getElementById("activity-chart-main"),o=window.matchMedia("(min-width: 1024px)").matches?t:s;if(o)try{const $=((await(await fetch(`/bff/users/${h}/activity-history?days=${n}`)).json()).activities||[]).map(u=>({date:u.date,discussions:u.discussionCount??0,posts:u.postCount??0,total:(u.discussionCount??0)+(u.postCount??0)})),g=o.clientHeight,y=g>0?Math.max(g-40,60):150;r(o,$,n,y)}catch(w){console.error("Error loading activity chart:",w),o.innerHTML='<div class="text-center py-8 text-error">Failed to load activity chart</div>'}}function r(n,t,s,i=150){if(!t||t.length===0){n.innerHTML=`
                    <div class="text-center py-12">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                        </svg>
                        <h3 class="font-semibold mb-2">No activity yet</h3>
                        <p class="text-sm text-muted">Activity will appear here once this user starts contributing</p>
                    </div>
                `;return}const o=Math.max(...t.map(e=>e.total),1),w=s>30;let x=t;if(w){const e=[];for(let l=0;l<t.length;l+=7){const d=t.slice(l,l+7);if(d.length===0||!d[0])continue;const k={date:d[0].date,discussions:d.reduce((m,p)=>m+p.discussions,0),posts:d.reduce((m,p)=>m+p.posts,0),total:d.reduce((m,p)=>m+p.total,0),isWeek:!0};e.push(k)}x=e}const $=x.map(e=>{const l=o>0?e.total/o*100:0,d=e.total>0?e.discussions/e.total*100:0,k=e.total>0?e.posts/e.total*100:0,p={timeZone:window.snakkTimezone||"UTC",month:"short",day:"numeric"},T=H=>{try{return new Date(H).toLocaleDateString("en-US",p)}catch{return new Date(H).toLocaleDateString("en-US",{month:"short",day:"numeric"})}},D=w?`Week of ${T(e.date)}`:T(e.date);return`
                    <div class="activity-chart-bar-wrapper">
                        <div class="activity-chart-bar-container" style="height: ${i}px;">
                            <div class="activity-chart-bar"
                                 style="height: ${e.total===0?"4px":l+"%"}; ${e.total===0?"min-height: 4px;":""}"
                                 title="${e.total} contribution${e.total!==1?"s":""}\\n${e.discussions} discussion${e.discussions!==1?"s":""}\\n${e.posts} post${e.posts!==1?"s":""}\\n${D}">
                                ${e.discussions>0?`<div class="activity-chart-bar-segment-primary" style="height: ${d}%;"></div>`:""}
                                ${e.posts>0?`<div class="activity-chart-bar-segment-secondary" style="height: ${k}%;"></div>`:""}
                                ${e.total===0?'<div class="activity-chart-bar-zero"></div>':""}
                            </div>
                        </div>
                    </div>
                `}).join(""),g=t.reduce((e,l)=>e+l.discussions,0),y=t.reduce((e,l)=>e+l.posts,0),u=g+y;n.innerHTML=`
                <div class="space-y-4">
                    <div class="activity-chart-wrapper" style="height: ${i+40}px;">
                        ${$}
                    </div>
                    <div class="activity-chart-legend">
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-primary"></div>
                            <span>${g} discussions</span>
                        </div>
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-secondary"></div>
                            <span>${y} posts</span>
                        </div>
                        <span class="text-base-content/50">(${u} total)</span>
                    </div>
                </div>
            `}async function b(){const n=document.getElementById("name-history-section"),t=document.getElementById("name-history-list");if(!(!n||!t))try{const i=await(await fetch("/bff/me/display-name-history",{credentials:"include"})).json();if(!i.entries||i.entries.length===0)return;t.innerHTML=i.entries.map(o=>`
                    <div class="flex items-center justify-between text-sm py-1.5">
                        <div class="min-w-0">
                            <span class="text-base-content/50 line-through">${f(o.previousName)}</span>
                            <span class="text-base-content/40 mx-1">&rarr;</span>
                            <span class="font-medium">${f(o.newName)}</span>
                        </div>
                        <span class="text-xs text-base-content/40 shrink-0 ml-2">${L(o.changedAt)}</span>
                    </div>
                `).join(""),n.classList.remove("hidden")}catch(s){console.error("Error loading name history:",s)}}async function B(){const n=document.getElementById("profile-actions");if(n)try{const s=await(await fetch("/bff/auth/status",{credentials:"include"})).json();if(!s.isAuthenticated){n.innerHTML="";return}if(s.publicId===h){n.innerHTML=`
                        <a href="/settings" class="btn btn-outline btn-sm">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                            </svg>
                            Edit Profile
                        </a>
                    `,b();return}const o=await(await fetch(`/bff/users/${h}/follow-status?currentUserId=${s.publicId}`,{credentials:"include"})).json();n.innerHTML=`
                    <button data-action="toggle-follow-user"
                            data-user-id="${h}"
                            class="btn btn-outline btn-sm"
                            id="follow-btn">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="${o.isFollowing?"M5 13l4 4L19 7":"M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"}" />
                        </svg>
                        <span id="follow-btn-text">${o.isFollowing?"Following":"Follow"}</span>
                    </button>
                `}catch(t){console.error("Error loading profile actions:",t),n.innerHTML=""}}async function z(n){const t=document.getElementById("follow-btn"),s=document.getElementById("follow-btn-text");if(!(!t||!s)){t.disabled=!0;try{const i=await fetch(`/bff/users/${n}/follow`,{method:"POST",credentials:"include"});if(i.ok){const o=await i.json();s.textContent=o.isFollowing?"Following":"Follow",a()}else throw new Error("Failed to toggle follow")}catch(i){console.error("Error toggling follow:",i),alert("Failed to update follow status")}finally{t.disabled=!1}}}a(),B(),v(365),c(5),document.addEventListener("click",async n=>{const t=n.target;if(!t)return;const s=t.closest("[data-action]");if(!s||!s.dataset.action)return;switch(s.dataset.action){case"toggle-follow-user":n.preventDefault(),s.dataset.userId&&await z(s.dataset.userId);break;case"load-activity-chart":n.preventDefault(),s.dataset.days&&await v(parseInt(s.dataset.days,10));break}})}function j(){const a=document.querySelectorAll(".fp-profile-tab");a.forEach(c=>{c.addEventListener("click",()=>{const v=c.dataset.tab;a.forEach(r=>{r.classList.toggle("active",r===c),r.setAttribute("aria-selected",r===c?"true":"false")}),document.querySelectorAll(".fp-profile-tab-panel").forEach(r=>{r.hidden=r.id!=="tab-"+v})})})}S(),j()})();
