"use strict";(function(){"use strict";const p=window.SnakkUtils?.escapeHtml||function(r){if(!r)return"";const c=document.createElement("div");return c.textContent=r,c.innerHTML},$=window.SnakkUtils?.sanitizeHtml||function(r){if(!r)return"";const d=new DOMParser().parseFromString(r,"text/html");return d.querySelectorAll("script,iframe,object,embed,form,base,meta,link,style").forEach(a=>a.remove()),d.body.querySelectorAll("*").forEach(a=>{Array.from(a.attributes).forEach(u=>{u.name.startsWith("on")&&a.removeAttribute(u.name)}),["href","src","action","formaction"].forEach(u=>{const b=a.getAttribute(u);b&&b.trim().toLowerCase().startsWith("javascript:")&&a.removeAttribute(u)})}),d.body.innerHTML},g=window.SnakkUtils?.sanitizeUrl||function(r){if(!r)return"#";const c=r.trim().toLowerCase();return c.startsWith("javascript:")||c.startsWith("data:")?"#":r},y=window.SnakkUtils?.formatRelativeTime||function(r){const c=new Date(r),a=Math.floor((new Date().getTime()-c.getTime())/1e3);if(a<60)return"just now";if(a<3600)return`${Math.floor(a/60)}m ago`;if(a<86400)return`${Math.floor(a/3600)}h ago`;if(a<604800)return`${Math.floor(a/86400)}d ago`;const u=window.snakkTimezone||"UTC";try{return c.toLocaleDateString("en-US",{timeZone:u,month:"short",day:"numeric",year:"numeric"})}catch{return c.toLocaleDateString("en-US",{month:"short",day:"numeric",year:"numeric"})}};function C(r,c,d){async function a(){try{const t=await(await fetch(`/bff/users/${r}/stats`)).json(),e=document.getElementById("stat-followers");e&&(e.textContent=t.followerCount||0);const s=document.getElementById("stat-replies");s&&t.replyCount!==void 0&&(s.textContent=t.replyCount)}catch(n){console.error("Error loading user stats:",n);const t=document.getElementById("stat-followers");t&&(t.textContent="0")}}async function u(n){const t=document.getElementById("recent-discussions");if(t)try{const s=await(await fetch(`/bff/search/discussions?authorPublicId=${r}&pageSize=${n}`)).json();if(!s.items||s.items.length===0){t.innerHTML=`
                        <div class="text-center py-8 text-muted">
                            <p>No discussions yet</p>
                        </div>
                    `;return}t.innerHTML=s.items.map(i=>`
                    <a href="${g(i.url)}" class="block hover:bg-base-200 p-3 rounded transition-colors">
                        <h4 class="font-medium mb-1">${p(i.title)}</h4>
                        <div class="flex items-center gap-4 text-sm text-muted">
                            <span>${i.replyCount} ${i.replyCount===1?"reply":"replies"}</span>
                            <span>${y(i.createdAt)}</span>
                        </div>
                    </a>
                `).join("")}catch(e){console.error("Error loading discussions:",e),t.innerHTML='<div class="text-center py-8 text-error">Failed to load discussions</div>'}}async function b(n){const t=document.getElementById("recent-posts");if(t)try{const s=await(await fetch(`/bff/search/posts?authorPublicId=${r}&pageSize=${n}`)).json();if(!s.items||s.items.length===0){t.innerHTML=`
                        <div class="text-center py-8 text-muted">
                            <p>No posts yet</p>
                        </div>
                    `;return}t.innerHTML=s.items.map(i=>`
                    <a href="${g(i.discussionUrl)}" class="block hover:bg-base-200 p-3 rounded transition-colors">
                        <div class="prose prose-sm max-w-none mb-2">
                            ${$(i.contentPreview)}
                        </div>
                        <div class="flex items-center gap-4 text-sm text-muted">
                            <span>in ${p(i.discussionTitle)}</span>
                            <span>${y(i.createdAt)}</span>
                        </div>
                    </a>
                `).join("")}catch(e){console.error("Error loading posts:",e),t.innerHTML='<div class="text-center py-8 text-error">Failed to load posts</div>'}}async function E(){const n=document.getElementById("all-discussions");if(n)try{const e=await(await fetch(`/bff/search/discussions?authorPublicId=${r}&pageSize=20`)).json();if(!e.items||e.items.length===0){n.innerHTML=`
                        <div class="text-center py-12">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z" />
                            </svg>
                            <h3 class="font-semibold mb-2">No discussions yet</h3>
                            <p class="text-sm text-muted">This user hasn't started any discussions</p>
                        </div>
                    `;return}n.innerHTML=e.items.map(s=>`
                    <div class="clean-card hover:shadow-md transition-shadow">
                        <a href="${g(s.url)}" class="block p-4">
                            <h3 class="font-semibold mb-2">${p(s.title)}</h3>
                            <div class="flex items-center gap-4 text-sm text-muted">
                                <span>${s.replyCount} ${s.replyCount===1?"reply":"replies"}</span>
                                <span>${y(s.createdAt)}</span>
                                <span class="ml-auto">${p(s.spaceName)}</span>
                            </div>
                        </a>
                    </div>
                `).join("")}catch(t){console.error("Error loading all discussions:",t),n.innerHTML='<div class="text-center py-8 text-error">Failed to load discussions</div>'}}async function S(){const n=document.getElementById("all-posts");if(n)try{const e=await(await fetch(`/bff/search/posts?authorPublicId=${r}&pageSize=20`)).json();if(!e.items||e.items.length===0){n.innerHTML=`
                        <div class="text-center py-12">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 8h10M7 12h4m1 8l-4-4H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-3l-4 4z" />
                            </svg>
                            <h3 class="font-semibold mb-2">No posts yet</h3>
                            <p class="text-sm text-muted">This user hasn't made any posts</p>
                        </div>
                    `;return}n.innerHTML=e.items.map(s=>`
                    <div class="clean-card hover:shadow-md transition-shadow">
                        <a href="${g(s.discussionUrl)}" class="block p-4">
                            <div class="prose prose-sm max-w-none mb-3">
                                ${$(s.contentPreview)}
                            </div>
                            <div class="flex items-center gap-4 text-sm text-muted">
                                <span>in ${p(s.discussionTitle)}</span>
                                <span>${y(s.createdAt)}</span>
                                <span class="ml-auto">${p(s.spaceName)}</span>
                            </div>
                        </a>
                    </div>
                `).join("")}catch(t){console.error("Error loading all posts:",t),n.innerHTML='<div class="text-center py-8 text-error">Failed to load posts</div>'}}async function k(n){["14","30","90"].forEach(e=>{const s=document.getElementById(`chart-${e}`);s&&(e===n.toString()?s.classList.add("btn-active"):s.classList.remove("btn-active"))});const t=document.getElementById("activity-chart");if(t)try{const s=await(await fetch(`/bff/users/${r}/activity-history?days=${n}`)).json();j(t,s.data,n)}catch(e){console.error("Error loading activity chart:",e),t.innerHTML='<div class="text-center py-8 text-error">Failed to load activity chart</div>'}}function j(n,t,e){if(!t||t.length===0){n.innerHTML=`
                    <div class="text-center py-12">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-16 w-16 mx-auto text-muted mb-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                        </svg>
                        <h3 class="font-semibold mb-2">No activity yet</h3>
                        <p class="text-sm text-muted">Activity will appear here once this user starts contributing</p>
                    </div>
                `;return}const s=Math.max(...t.map(o=>o.total),1),i=150,w=e>30;let f=t;if(w){const o=[];for(let l=0;l<t.length;l+=7){const m=t.slice(l,l+7);if(m.length===0||!m[0])continue;const x={date:m[0].date,discussions:m.reduce((v,h)=>v+h.discussions,0),posts:m.reduce((v,h)=>v+h.posts,0),total:m.reduce((v,h)=>v+h.total,0),isWeek:!0};o.push(x)}f=o}const D=f.map(o=>{const l=s>0?o.total/s*100:0,m=o.total>0?o.discussions/o.total*100:0,x=o.total>0?o.posts/o.total*100:0,h={timeZone:window.snakkTimezone||"UTC",month:"short",day:"numeric"},T=H=>{try{return new Date(H).toLocaleDateString("en-US",h)}catch{return new Date(H).toLocaleDateString("en-US",{month:"short",day:"numeric"})}},U=w?`Week of ${T(o.date)}`:T(o.date);return`
                    <div class="activity-chart-bar-wrapper">
                        <div class="activity-chart-bar-container" style="height: ${i}px;">
                            <div class="activity-chart-bar"
                                 style="height: ${o.total===0?"4px":l+"%"}; ${o.total===0?"min-height: 4px;":""}"
                                 title="${o.total} contribution${o.total!==1?"s":""}\\n${o.discussions} discussion${o.discussions!==1?"s":""}\\n${o.posts} post${o.posts!==1?"s":""}\\n${U}">
                                ${o.discussions>0?`<div class="activity-chart-bar-segment-primary" style="height: ${m}%;"></div>`:""}
                                ${o.posts>0?`<div class="activity-chart-bar-segment-secondary" style="height: ${x}%;"></div>`:""}
                                ${o.total===0?'<div class="activity-chart-bar-zero"></div>':""}
                            </div>
                        </div>
                    </div>
                `}).join(""),M=t.reduce((o,l)=>o+l.discussions,0),L=t.reduce((o,l)=>o+l.posts,0),F=M+L;n.innerHTML=`
                <div class="space-y-4">
                    <div class="activity-chart-wrapper" style="height: ${i+40}px;">
                        ${D}
                    </div>
                    <div class="activity-chart-legend">
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-primary"></div>
                            <span>${M} discussions</span>
                        </div>
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color activity-chart-legend-color-secondary"></div>
                            <span>${L} posts</span>
                        </div>
                        <div class="activity-chart-legend-item">
                            <div class="activity-chart-legend-color bg-accent"></div>
                            <span>${F} total</span>
                        </div>
                    </div>
                </div>
            `}async function z(){const n=document.getElementById("top-contributions");if(n)try{const e=await(await fetch(`/bff/search/discussions?authorPublicId=${r}&pageSize=3`)).json();if(!e.items||e.items.length===0){n.innerHTML=`
                        <div class="text-center py-6 text-muted">
                            <p>No discussions yet</p>
                        </div>
                    `;return}n.innerHTML=e.items.map((s,i)=>`
                    <div class="flex items-start gap-3">
                        <div class="flex-shrink-0 w-8 h-8 rounded-full bg-primary text-primary-content flex items-center justify-center font-semibold">
                            ${i+1}
                        </div>
                        <div class="flex-1 min-w-0">
                            <a href="${g(s.url)}" class="font-medium hover:underline block truncate">
                                ${p(s.title)}
                            </a>
                            <div class="text-sm text-muted">
                                ${s.replyCount} ${s.replyCount===1?"reply":"replies"}
                            </div>
                        </div>
                    </div>
                `).join("")}catch(t){console.error("Error loading top contributions:",t),n.innerHTML='<div class="text-center py-6 text-error">Failed to load</div>'}}async function A(){const n=document.getElementById("profile-actions");if(n)try{const e=await(await fetch("/bff/auth/status",{credentials:"include"})).json();if(!e.isAuthenticated){n.innerHTML="";return}if(e.publicId===r){n.innerHTML=`
                        <a href="/settings" class="btn btn-outline btn-sm">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                            </svg>
                            Edit Profile
                        </a>
                    `;return}const i=await(await fetch(`/bff/users/${r}/follow-status?currentUserId=${e.publicId}`,{credentials:"include"})).json();n.innerHTML=`
                    <button data-action="toggle-follow-user"
                            data-user-id="${r}"
                            class="btn ${i.isFollowing?"btn-outline":"btn-primary"} btn-sm"
                            id="follow-btn">
                        <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4 mr-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="${i.isFollowing?"M5 13l4 4L19 7":"M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z"}" />
                        </svg>
                        <span id="follow-btn-text">${i.isFollowing?"Following":"Follow"}</span>
                    </button>
                `}catch(t){console.error("Error loading profile actions:",t),n.innerHTML=""}}async function B(n){const t=document.getElementById("follow-btn"),e=document.getElementById("follow-btn-text");if(!(!t||!e)){t.disabled=!0;try{const s=await fetch(`/bff/users/${n}/follow`,{method:"POST",credentials:"include"});if(s.ok){const i=await s.json();e.textContent=i.isFollowing?"Following":"Follow",i.isFollowing?(t.classList.remove("btn-primary"),t.classList.add("btn-outline")):(t.classList.remove("btn-outline"),t.classList.add("btn-primary")),a()}else throw new Error("Failed to toggle follow")}catch(s){console.error("Error toggling follow:",s),alert("Failed to update follow status")}finally{t.disabled=!1}}}function P(){const n=document.getElementById("user-badges");if(!n)return;const t=[],e=d.totalActivity,s=d.daysSinceJoined,i=d.discussionCount,w=d.postCount;e>=1e3?t.push({text:"\u{1F3C6} Power User",color:"badge-warning",title:"1000+ contributions"}):e>=500?t.push({text:"\u2B50 Super Contributor",color:"badge-info",title:"500+ contributions"}):e>=100&&t.push({text:"\u2728 Active Member",color:"badge-success",title:"100+ contributions"}),i>=50&&t.push({text:"\u{1F4AC} Discussion Starter",color:"badge-primary",title:"50+ discussions"}),w>=100&&w>i*3&&t.push({text:"\u{1F5E3}\uFE0F Conversationalist",color:"badge-accent",title:"Highly engaged in discussions"}),s>=365?t.push({text:"\u{1F396}\uFE0F Veteran",color:"badge-secondary",title:"Member for over a year"}):s>=180&&t.push({text:"\u{1F4C5} Regular",color:"badge-neutral",title:"Member for 6+ months"}),t.length>0&&(n.innerHTML=t.map(f=>`<div class="badge ${f.color} badge-sm" title="${f.title}">${f.text}</div>`).join(""))}a(),A(),P(),c==="overview"?(k(30),z(),u(5),b(5)):c==="discussions"?E():c==="posts"&&S(),document.addEventListener("click",async n=>{const t=n.target;if(!t)return;const e=t.closest("[data-action]");if(!e||!e.dataset.action)return;switch(e.dataset.action){case"toggle-follow-user":n.preventDefault(),e.dataset.userId&&await B(e.dataset.userId);break;case"load-activity-chart":n.preventDefault(),e.dataset.days&&await k(parseInt(e.dataset.days,10));break}})}window.initializeProfile=C})();
