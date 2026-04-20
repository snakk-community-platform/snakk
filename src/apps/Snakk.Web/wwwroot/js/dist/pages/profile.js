"use strict";(function(){"use strict";const w=window.SnakkUtils?.escapeHtml||function(a){if(!a)return"";const c=document.createElement("div");return c.textContent=a,c.innerHTML},S=window.SnakkUtils?.sanitizeHtml||function(a){if(!a)return"";const d=new DOMParser().parseFromString(a,"text/html");return d.querySelectorAll("script,iframe,object,embed,form,base,meta,link,style").forEach(n=>n.remove()),d.body.querySelectorAll("*").forEach(n=>{Array.from(n.attributes).forEach(u=>{u.name.startsWith("on")&&n.removeAttribute(u.name)}),["href","src","action","formaction"].forEach(u=>{const b=n.getAttribute(u);b&&b.trim().toLowerCase().startsWith("javascript:")&&n.removeAttribute(u)})}),d.body.innerHTML},C=window.SnakkUtils?.sanitizeUrl||function(a){if(!a)return"#";const c=a.trim().toLowerCase();return c.startsWith("javascript:")||c.startsWith("data:")?"#":a},L=window.SnakkUtils?.formatRelativeTime||function(a){const c=new Date(a),n=Math.floor((new Date().getTime()-c.getTime())/1e3);if(n<60)return"just now";if(n<3600)return`${Math.floor(n/60)}m ago`;if(n<86400)return`${Math.floor(n/3600)}h ago`;if(n<604800)return`${Math.floor(n/86400)}d ago`;const u=window.snakkTimezone||"UTC";try{return c.toLocaleDateString("en-US",{timeZone:u,month:"short",day:"numeric",year:"numeric"})}catch{return c.toLocaleDateString("en-US",{month:"short",day:"numeric",year:"numeric"})}},M=document.getElementById("profile-page-config");if(!M)return;const f=JSON.parse(M.textContent||"{}").userId;function I(){async function a(){try{const t=await(await fetch(`/bff/users/${f}/stats`)).json(),s=document.getElementById("stat-followers");s&&(s.textContent=t.followerCount||0);const r=document.getElementById("stat-replies");r&&t.replyCount!==void 0&&(r.textContent=t.replyCount)}catch(o){console.error("Error loading user stats:",o);const t=document.getElementById("stat-followers");t&&(t.textContent="0")}}async function c(o){const t=document.getElementById("recent-posts");if(t)try{const r=await(await fetch(`/bff/search/posts?authorPublicId=${f}&pageSize=${o}`)).json();if(!r.items||r.items.length===0){t.innerHTML=`
                        <div class="text-center py-8 text-muted">
                            <p>No posts yet</p>
                        </div>
                    `;return}t.innerHTML=`<div class="topic-list">${r.items.map(i=>`
                    <div class="topic-item-wrapper">
                        <div class="topic-item">
                            <div class="topic-content">
                                <div class="topic-title">
                                    <a href="${C(i.url)}" class="topic-title-link">${w(i.discussionTitle)}</a>
                                </div>
                                <div class="topic-meta">
                                    <span class="topic-meta-link">${w(i.hubName)}</span>
                                    <svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="9 18 15 12 9 6"></polyline></svg>
                                    <span class="topic-meta-link">${w(i.spaceName)}</span>
                                    <span class="topic-meta-separator">&middot;</span>
                                    <span>${L(i.createdAt)}</span>
                                </div>
                                <div class="prose prose-sm max-w-none mt-1 text-sm text-base-content/70 fp-post-preview">
                                    ${S(i.contentPreview)}
                                </div>
                            </div>
                            <a href="${C(i.url)}" class="topic-latest-link" title="Go to discussion">
                                <svg class="chevron-right" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <polyline points="6 9 12 15 18 9"></polyline>
                                </svg>
                            </a>
                        </div>
                    </div>
                `).join("")}</div>`}catch(s){console.error("Error loading posts:",s),t.innerHTML='<div class="text-center py-8 text-error">Failed to load posts</div>'}}async function d(o){const t=document.getElementById("activity-chart-sidebar"),s=document.getElementById("activity-chart-main"),i=window.matchMedia("(min-width: 1024px)").matches?t:s;if(i)try{const $=((await(await fetch(`/bff/users/${f}/activity-history?days=${o}`)).json()).activities||[]).map(p=>({date:p.date,discussions:p.discussionCount??0,posts:p.postCount??0,total:(p.discussionCount??0)+(p.postCount??0)})),y=i.clientHeight,x=y>0?Math.max(y-40,60):150;n(i,$,o,x)}catch(g){console.error("Error loading activity chart:",g),i.innerHTML='<div class="text-center py-8 text-error">Failed to load activity chart</div>'}}function n(o,t,s,r=150){if(!t||t.length===0){o.innerHTML=`
                    <div class="text-center py-12">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                        </svg>
                        <h3 class="font-semibold mb-2">No activity yet</h3>
                        <p class="text-sm text-muted">Activity will appear here once this user starts contributing</p>
                    </div>
                `;return}const i=Math.max(...t.map(e=>e.total),1),g=s>30;let k=t;if(g){const e=[];for(let l=0;l<t.length;l+=7){const h=t.slice(l,l+7);if(h.length===0||!h[0])continue;const E={date:h[0].date,discussions:h.reduce((v,m)=>v+m.discussions,0),posts:h.reduce((v,m)=>v+m.posts,0),total:h.reduce((v,m)=>v+m.total,0),isWeek:!0};e.push(E)}k=e}const $=k.map(e=>{const l=i>0?e.total/i*100:0,h=e.total>0?e.discussions/e.total*100:0,E=e.total>0?e.posts/e.total*100:0,m={timeZone:window.snakkTimezone||"UTC",month:"short",day:"numeric"},T=H=>{try{return new Date(H).toLocaleDateString("en-US",m)}catch{return new Date(H).toLocaleDateString("en-US",{month:"short",day:"numeric"})}},z=g?`Week of ${T(e.date)}`:T(e.date);return`
                    <div class="activity-chart-bar-wrapper">
                        <div class="activity-chart-bar-container" style="height: ${r}px;">
                            <div class="activity-chart-bar"
                                 style="height: ${e.total===0?"4px":l+"%"}; ${e.total===0?"min-height: 4px;":""}"
                                 title="${e.total} contribution${e.total!==1?"s":""}\\n${e.discussions} discussion${e.discussions!==1?"s":""}\\n${e.posts} post${e.posts!==1?"s":""}\\n${z}">
                                ${e.discussions>0?`<div class="activity-chart-bar-segment-primary" style="height: ${h}%;"></div>`:""}
                                ${e.posts>0?`<div class="activity-chart-bar-segment-secondary" style="height: ${E}%;"></div>`:""}
                                ${e.total===0?'<div class="activity-chart-bar-zero"></div>':""}
                            </div>
                        </div>
                    </div>
                `}).join(""),y=t.reduce((e,l)=>e+l.discussions,0),x=t.reduce((e,l)=>e+l.posts,0),p=y+x;o.innerHTML=`
                <div class="space-y-4">
                    <div class="activity-chart-wrapper" style="height: ${r+40}px;">
                        ${$}
                    </div>
                    <div class="activity-chart-legend">
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-primary"></div>
                            <span>${y} discussions</span>
                        </div>
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-secondary"></div>
                            <span>${x} posts</span>
                        </div>
                        <span class="text-base-content/50">(${p} total)</span>
                    </div>
                </div>
            `}async function u(){const o=document.getElementById("name-history-section"),t=document.getElementById("name-history-list");if(!(!o||!t))try{const r=await(await fetch("/bff/me/display-name-history",{credentials:"include"})).json();if(!r.entries||r.entries.length===0)return;t.innerHTML=r.entries.map(i=>`
                    <div class="flex items-center justify-between text-sm py-1.5">
                        <div class="min-w-0">
                            <span class="text-base-content/50 line-through">${w(i.previousName)}</span>
                            <span class="text-base-content/40 mx-1">&rarr;</span>
                            <span class="font-medium">${w(i.newName)}</span>
                        </div>
                        <span class="text-xs text-base-content/40 shrink-0 ml-2">${L(i.changedAt)}</span>
                    </div>
                `).join(""),o.classList.remove("hidden")}catch(s){console.error("Error loading name history:",s)}}async function b(){const o=document.getElementById("profile-actions");if(o)try{const s=await(await fetch("/bff/auth/status",{credentials:"include"})).json();if(!s.isAuthenticated){o.innerHTML="";return}if(s.publicId===f){o.innerHTML=`
                        <a href="/settings" class="btn btn-outline btn-sm">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                            </svg>
                            Edit Profile
                        </a>
                    `,u();return}const i=await(await fetch(`/bff/users/${f}/follow-status?currentUserId=${s.publicId}`,{credentials:"include"})).json();o.innerHTML=`
                    <button data-action="toggle-follow-user"
                            data-user-id="${f}"
                            class="btn btn-outline btn-sm"
                            id="follow-btn">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="${i.isFollowing?"M5 13l4 4L19 7":"M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"}" />
                        </svg>
                        <span id="follow-btn-text">${i.isFollowing?"Following":"Follow"}</span>
                    </button>
                `}catch(t){console.error("Error loading profile actions:",t),o.innerHTML=""}}async function B(o){const t=document.getElementById("follow-btn"),s=document.getElementById("follow-btn-text");if(!(!t||!s)){t.disabled=!0;try{const r=await fetch(`/bff/users/${o}/follow`,{method:"POST",credentials:"include"});if(r.ok){const i=await r.json();s.textContent=i.isFollowing?"Following":"Follow",a()}else throw new Error("Failed to toggle follow")}catch(r){console.error("Error toggling follow:",r),alert("Failed to update follow status")}finally{t.disabled=!1}}}a(),b(),d(365),c(5),document.addEventListener("click",async o=>{const t=o.target;if(!t)return;const s=t.closest("[data-action]");if(!s||!s.dataset.action)return;switch(s.dataset.action){case"toggle-follow-user":o.preventDefault(),s.dataset.userId&&await B(s.dataset.userId);break;case"load-activity-chart":o.preventDefault(),s.dataset.days&&await d(parseInt(s.dataset.days,10));break}})}function j(){const a=document.querySelectorAll(".fp-profile-tab");a.forEach(c=>{c.addEventListener("click",()=>{const d=c.dataset.tab;a.forEach(n=>{n.classList.toggle("active",n===c),n.setAttribute("aria-selected",n===c?"true":"false")}),document.querySelectorAll(".fp-profile-tab-panel").forEach(n=>{n.hidden=n.id!=="tab-"+d})})})}I(),j()})();
