"use strict";class FrontpageDiscussions{constructor(e={}){this.pageSize=e.pageSize||30,this.scrollSentinelMargin=e.scrollSentinelMargin||"200px",this.previewMaxLength=e.previewMaxLength||480,this.nextCursor="",this.hasMore=!1,this.scrollObserver=null,this.loadMoreRequest=null,this.previewHandlersAttached=!1,this.previewCache=new Map,this.scrollToTopBtn=null,this.scrollCounter=null,this.initialDiscussionCount=0}init(){const e=document.getElementById("discussions-container"),t=document.getElementById("endless-scroll-sentinel");if(!e||!t)return;const n=window.SnakkConfig;!n||!n.discussions||(this.nextCursor=n.discussions.nextCursor||"",this.hasMore=n.discussions.hasMoreItems||!1,this.initialDiscussionCount=e.querySelectorAll(".topic-item-wrapper").length,this.scrollObserver&&this.scrollObserver.disconnect(),this.scrollObserver=new IntersectionObserver(s=>{const i=s[0];i&&i.isIntersecting&&!this.loadMoreRequest&&this.hasMore&&this.loadMore()},{rootMargin:this.scrollSentinelMargin}),this.scrollObserver.observe(t),this.previewHandlersAttached||(this.attachPreviewHandlers(e),this.previewHandlersAttached=!0),this.initScrollToTop())}truncateText(e,t){if(e.length<=t)return e;let n=e.substring(0,t);const s=n.lastIndexOf(" ");return s>0&&(n=n.substring(0,s)),n+"..."}async fetchPreview(e){if(this.previewCache.has(e)){const t=this.previewCache.get(e);return t!==void 0?t:null}try{const t=await fetch(`/bff/discussions/${e}/preview`);if(!t.ok)throw new Error("Failed to fetch preview");const n=await t.json();return this.previewCache.set(e,n.content),n.content}catch(t){return console.error("[FrontpageDiscussions] Error fetching preview:",t),null}}async togglePreview(e,t,n){if(!t.classList.contains("hidden"))t.classList.add("hidden"),e.classList.remove("active");else{const i=t.querySelector(".preview-content");if(!i)return;if(i.textContent)t.classList.remove("hidden"),e.classList.add("active");else{const r=document.createElement("span");r.className="loading loading-spinner loading-sm",i.replaceChildren(r),t.classList.remove("hidden"),e.classList.add("active");const o=await this.fetchPreview(n);if(o){const l=this.truncateText(o,this.previewMaxLength);i.textContent=l}else i.textContent="Failed to load preview"}}}attachPreviewHandlers(e){e.addEventListener("click",t=>{const s=t.target.closest(".preview-btn");if(!s||!s.dataset.discussionId)return;t.preventDefault();const i=s.dataset.discussionId,o=s.closest(".topic-item-wrapper")?.nextElementSibling;o&&o.classList.contains("discussion-preview")&&this.togglePreview(s,o,i)})}createDiscussionElement(e){const{formatRelativeTime:t,formatDiscussionBadges:n,escapeHtml:s}=window.SnakkUtils||{},i=window.SnakkConfig,r=i.showCommunityInDiscussionList&&e.community?.slug?`/c/${e.community.slug}`:i.communityPrefix,o=`${r}/h/${e.hub.slug}/${e.space.slug}/${e.slug}~${e.publicId}`,l=`${o}?gotoUnread=true`,a=e.space.avatarUrl,c=t(e.lastActivityAt||e.createdAt),d=n(e);let p="";i.showCommunityInDiscussionList&&e.community&&(p=`
                <a href="/c/${e.community.slug}"
                   class="topic-meta-link"
                   data-popup-type="community"
                   data-popup-id="${e.community.publicId}"
                   data-popup-name="${s(e.community.name)}">${s(e.community.name)}</a>
                <span class="topic-meta-separator">/</span>
            `);const u=`
            <div class="topic-item-wrapper">
                <img src="${a}"
                     alt="${s(e.space.name)}"
                     loading="lazy"
                     class="w-10 h-10 rounded-full flex-shrink-0 mr-2 hidden sm:block" />

                <div class="topic-item">
                    <div class="topic-content">
                        <div class="topic-title">
                            <a href="${o}" class="topic-title-link">${s(e.title)}</a>${d}
                        </div>
                        <div class="topic-meta">
                            ${p}
                            <a href="${r}/h/${e.hub.slug}"
                               class="topic-meta-link"
                               data-popup-type="hub"
                               data-popup-id="${e.hub.publicId}"
                               data-popup-name="${s(e.hub.name)}">${s(e.hub.name)}</a>
                            <span class="topic-meta-separator">/</span>
                            <a href="${r}/h/${e.hub.slug}/${e.space.slug}"
                               class="topic-meta-link"
                               data-popup-type="space"
                               data-popup-id="${e.space.publicId}"
                               data-popup-name="${s(e.space.name)}">${s(e.space.name)}</a>
                            <span class="topic-meta-separator">\xB7</span>
                            <span>${c}</span>
                        </div>
                    </div>
                    <div class="topic-stats hidden sm:flex">
                        <div class="topic-stat">
                            <div class="topic-stat-icon">
                                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3zM7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"></path>
                                </svg>
                            </div>
                            <div class="topic-stat-value">${e.reactionCount||0}</div>
                        </div>
                        <div class="topic-stat">
                            <div class="topic-stat-icon">
                                <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                                    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path>
                                </svg>
                            </div>
                            <div class="topic-stat-value">${e.postCount||0}</div>
                        </div>
                    </div>
                </div>
                <button class="preview-btn"
                        data-discussion-id="${e.publicId}"
                        title="Preview first post"
                        type="button">
                    <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <polyline points="6 9 12 15 18 9"></polyline>
                    </svg>
                </button>
                <a href="${l}" class="topic-latest-link" title="Jump to first unread post">
                    <svg class="chevron-right" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <polyline points="6 9 12 15 18 9"></polyline>
                    </svg>
                </a>
            </div>
            <div class="discussion-preview hidden" data-discussion-id="${e.publicId}">
                <div class="preview-content"></div>
            </div>
        `,h=document.createElement("template");return h.innerHTML=u.trim(),h.content}initScrollToTop(){const e=document.getElementById("scroll-to-top-wrapper");if(this.scrollToTopBtn=document.getElementById("scroll-to-top-btn"),this.scrollCounter=document.getElementById("scroll-counter"),!e||!this.scrollToTopBtn)return;this.updateScrollCounter();let t;window.addEventListener("scroll",()=>{t!==void 0&&window.cancelAnimationFrame(t),t=window.requestAnimationFrame(()=>{this.handleScrollPosition()})},{passive:!0}),this.scrollToTopBtn.addEventListener("click",()=>{window.scrollTo({top:0,behavior:"smooth"})}),this.handleScrollPosition()}handleScrollPosition(){const e=document.getElementById("scroll-to-top-wrapper");if(!e)return;const t=window.pageYOffset||document.documentElement.scrollTop,n=document.getElementById("discussions-container");if(!n)return;const s=n.querySelectorAll(".topic-item-wrapper").length;t>800||s>this.initialDiscussionCount?e.classList.remove("hidden"):e.classList.add("hidden")}updateScrollCounter(){if(!this.scrollCounter)return;const e=document.getElementById("discussions-container");if(!e)return;const t=e.querySelectorAll(".topic-item-wrapper").length;this.scrollCounter.textContent=t.toString()}async loadMore(){if(this.loadMoreRequest||!this.hasMore)return;const e=window.SnakkConfig,t=document.getElementById("discussions-container"),n=document.getElementById("endless-scroll-sentinel"),s=document.getElementById("loading-indicator"),i=document.getElementById("end-of-list");if(!(!t||!n)){s?.classList.remove("hidden");try{const r=e.discussions;if(!r)return;let o=`/bff/discussions/recent?offset=0&pageSize=${this.pageSize}`;r.communityId&&(o+=`&communityId=${r.communityId}`),this.nextCursor&&(o+=`&cursor=${encodeURIComponent(this.nextCursor)}`),this.loadMoreRequest=fetch(o,{credentials:"include"});const l=await this.loadMoreRequest;if(!l.ok)throw new Error(`HTTP ${l.status}: ${l.statusText}`);const a=await l.json();a.items&&a.items.length>0?(a.items.forEach(c=>{const d=this.createDiscussionElement(c);t.appendChild(d)}),this.nextCursor=a.nextCursor||"",this.hasMore=a.hasMoreItems&&!!this.nextCursor,this.updateScrollCounter(),this.handleScrollPosition()):(this.hasMore=!1,this.nextCursor=""),this.hasMore||(n.classList.add("hidden"),i?.classList.remove("hidden"))}catch(r){console.error("[FrontpageDiscussions] Failed to load more discussions:",r);const o=document.createElement("div");o.className="alert alert-error my-4",o.innerHTML=`
                <svg xmlns="http://www.w3.org/2000/svg" class="stroke-current shrink-0 h-6 w-6" fill="none" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <span>Failed to load more discussions. Please refresh the page.</span>
            `,t.appendChild(o),this.hasMore=!1}finally{this.loadMoreRequest=null,s?.classList.add("hidden")}}}clearPreviewCache(){this.previewCache.clear()}destroy(){this.scrollObserver&&(this.scrollObserver.disconnect(),this.scrollObserver=null),this.previewCache.clear(),this.loadMoreRequest=null,this.scrollToTopBtn=null,this.scrollCounter=null}}window.FrontpageDiscussions=FrontpageDiscussions,window.SnakkFrontpageDiscussions=new FrontpageDiscussions,document.addEventListener("DOMContentLoaded",()=>{window.SnakkFrontpageDiscussions.init()}),document.body.addEventListener("htmx:load",()=>{document.getElementById("discussions-container")&&window.SnakkFrontpageDiscussions.init()});
